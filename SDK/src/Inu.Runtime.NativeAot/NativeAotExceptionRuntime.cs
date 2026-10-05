using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Inu.Runtime.NativeAot;

/// <summary>
/// Freestanding Windows-x64 NativeAOT exception dispatcher. It consumes the PE/COFF
/// runtime-function table and NativeAOT compressed EH clauses emitted by ILC; no OS
/// unwinder or Windows runtime library is linked.
/// </summary>
public static unsafe class NativeAotExceptionRuntime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct EhContext
    {
        public UInt64 Ip;
        public UInt64 Sp;
        public UInt64 Rbx;
        public UInt64 Rbp;
        public UInt64 Rsi;
        public UInt64 Rdi;
        public UInt64 R12;
        public UInt64 R13;
        public UInt64 R14;
        public UInt64 R15;
    }

#pragma warning disable CS0649 // PE .pdata is interpreted through this unmanaged overlay.
    private struct RuntimeFunction
    {
        public UInt32 Begin;
        public UInt32 End;
        public UInt32 Unwind;
    }
#pragma warning restore CS0649

    private struct Clause
    {
        public UInt32 Kind;
        public UInt32 TryStart;
        public UInt32 TryEnd;
        public UInt64 Handler;
        public UInt64 Filter;
        public UInt64 TargetType;
    }

    private const Byte RootKind = 0;
    private const Byte HasEhInfo = 0x04;
    private const Byte HasAssociatedData = 0x10;
    private const Byte UnwFlagEHandler = 0x01;
    private const Byte UnwFlagUHandler = 0x02;
    private const Byte UnwFlagChainInfo = 0x04;

    private static Object _activeException;
    private static EhContext _activeCatchContext;
    private static UInt64 _activeCatchHandler;
    private static Boolean _activeCatchContextValid;
    private static EhContext _activeRethrowContext;
    private static UInt64 _activeRethrowHandler;
    private static Boolean _activeRethrowContextValid;

#pragma warning disable CS0626 // NativeAOT binds these RuntimeImport methods directly to freestanding x64 shim symbols.
    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhCallFinally")]
    private static extern void CallFinally(UInt64 handler, EhContext* context);

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhCallCatch")]
    private static extern UInt64 CallCatch(Object exception, UInt64 handler, EhContext* context);

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhResume")]
    private static extern void Resume(UInt64 resumeIp, EhContext* context);
#pragma warning restore CS0626

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhTrace")]
    private static extern void TraceNative(UInt64 code);

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhTraceValue")]
    private static extern void TraceValueNative(UInt64 tag, UInt64 value);

    [MethodImpl(MethodImplOptions.InternalCall)]
    [RuntimeImport("*", "InuEhTraceEmergency")]
    private static extern void TraceEmergencyNative(UInt64 code);

    public static Boolean ConfigureImageBase(UInt64 imageBase)
    {
        if (imageBase == 0UL || !IsPeImageHeader(imageBase)) return false;
        _resolvedImageBase = imageBase;
        TraceNative(0x90UL);
        return true;
    }

    public static void TraceStage(UInt64 code) => TraceNative(code);

    /// <summary>Serial-only early-runtime diagnostic value. Emits EV:XX=XXXXXXXXXXXXXXXX.</summary>
    public static void TraceValue(UInt64 tag, UInt64 value) => TraceValueNative(tag, value);

    [RuntimeExport("InuRhThrowEx")]
    private static void Throw(Object exception, EhContext* throwContext)
    {
        TraceNative(0xA1UL);
        if (exception == null || throwContext == null) FailFast();
        TraceValueNative(0xE1UL, global::Inu.Runtime.RuntimeDiagnostics.GetMethodTableAddress(exception));
        TraceValueNative(0xE2UL, throwContext->Ip);
        TraceValueNative(0xE3UL, throwContext->Sp);
        TraceValueNative(0xE4UL, throwContext->Rbp);
        _activeException = exception;
        TraceNative(0xA2UL);
        Dispatch(exception, throwContext);
        FailFast();
    }

    [RuntimeExport("InuRhRethrow")]
    private static void Rethrow(EhContext* throwContext)
    {
        Object exception = _activeException;
        if (exception == null || throwContext == null || !_activeCatchContextValid) FailFast();

        // RhpRethrow must continue with the next enclosing handler selected from the
        // original protected-region context. Re-running pass 1 from the catch funclet
        // (or reconstructing that relationship from its return address) is not sufficient
        // for nested NativeAOT funclets. Cache the next eligible catch before entering the
        // current handler, mirroring the active ExInfo/clause progression used by NativeAOT.
        TraceNative(0xA6UL);
        if (!_activeRethrowContextValid || _activeRethrowHandler == 0UL) FailFast();
        EhContext sourceContext = _activeCatchContext;
        EhContext selectedContext = _activeRethrowContext;
        UInt64 selectedHandler = _activeRethrowHandler;
        TraceNative(0xA8UL);

        // The selected enclosing catch can live in the same physical managed frame as
        // the catch issuing `throw;` (nested try/catch in one method). In that case there
        // is no frame transition to rediscover: invoke the cached enclosing clause directly
        // using the retained protected-region context. This mirrors NativeAOT's active
        // ExInfo clause progression and avoids attempting to unwind the catch funclet as a
        // new managed frame. Cross-frame rethrows continue through DispatchSelected.
        if (SamePhysicalFrame(ref sourceContext, ref selectedContext))
        {
            TraceNative(0xA9UL);
            DispatchSameFrameRethrow(exception, &sourceContext, &selectedContext, selectedHandler);
            FailFast();
        }

        DispatchSelected(exception, &sourceContext, &selectedContext, selectedHandler);
        FailFast();
    }

    private static void Dispatch(Object exception, EhContext* source)
        => Dispatch(exception, source, 0UL);

    private static void Dispatch(Object exception, EhContext* source, UInt64 skipHandler)
    {
        EhContext cursor = *source;
        TraceNative(0xA3UL);
        UInt64 imageBase = FindImageBase(cursor.Ip);
        if (imageBase == 0UL) FailFast();
        TraceNative(0xA4UL);

        RuntimeFunction* table;
        UInt32 count;
        if (!GetExceptionDirectory(imageBase, out table, out count)) FailFast();
        TraceNative(0xA5UL);

        EhContext selectedContext;
        UInt64 selectedHandler;
        if (!TryFindCatch(exception, ref cursor, imageBase, table, count, skipHandler, out selectedContext, out selectedHandler))
            FailFast();

        DispatchSelected(exception, &cursor, &selectedContext, selectedHandler);
        FailFast();
    }

    private static Boolean TryFindCatch(Object exception, ref EhContext cursor, UInt64 imageBase,
        RuntimeFunction* table, UInt32 count, UInt64 skipHandler,
        out EhContext selectedContext, out UInt64 selectedHandler)
    {
        selectedContext = default;
        selectedHandler = 0UL;
        EhContext scan = cursor;
        TraceNative(0xB0UL);
        TraceValueNative(0xD0UL, global::Inu.Runtime.RuntimeDiagnostics.GetMethodTableAddress(exception));
        TraceValueNative(0xD1UL, skipHandler);
        for (UInt32 depth = 0U; depth < 256U && scan.Ip != 0UL; depth++)
        {
            TraceValueNative(0xD2UL, depth);
            TraceValueNative(0xD3UL, scan.Ip);
            TraceValueNative(0xD4UL, scan.Sp);
            TraceValueNative(0xD5UL, scan.Rbp);

            RuntimeFunction* fn = FindFunction(imageBase, table, count, scan.Ip);
            if (fn == null)
            {
                // A missing RuntimeFunction is a leaf only while the PC is still owned by
                // NativeAOT's managed code range. 0.0.195 incorrectly applied the x64 leaf
                // rule to any address in the PE image and stepped through InuKernelEntry's
                // native bootstrap frame. Stop cleanly at that native boundary instead.
                TraceNative(0xBFUL);
                if (TryUnwindLeaf(imageBase, table, count, ref scan))
                {
                    TraceNative(0xB9UL);
                    TraceValueNative(0xE8UL, scan.Ip);
                    TraceValueNative(0xE9UL, scan.Sp);
                    TraceValueNative(0xEAUL, scan.Rbp);
                    continue;
                }
                break;
            }
            RuntimeFunction* root = FindRootFunction(imageBase, table, fn);
            UInt64 methodStart = imageBase + root->Begin;
            UInt32 codeOffset = scan.Ip > methodStart ? (UInt32)(scan.Ip - methodStart - 1UL) : 0U;
            TraceValueNative(0xD6UL, methodStart);
            TraceValueNative(0xD7UL, codeOffset);

            Byte* eh = GetEhInfo(imageBase, root);
            TraceValueNative(0xD8UL, (UInt64)(nuint)eh);
            if (eh != null)
            {
                Byte* p = eh;
                UInt32 clauses = ReadUnsigned(ref p);
                TraceValueNative(0xD9UL, clauses);
                for (UInt32 i = 0; i < clauses; i++)
                {
                    Clause clause = ReadClause(imageBase, methodStart, ref p);
                    TraceValueNative(0xDAUL, i);
                    TraceValueNative(0xDBUL, clause.Kind);
                    TraceValueNative(0xDCUL, clause.TryStart);
                    TraceValueNative(0xDDUL, clause.TryEnd);
                    TraceValueNative(0xDEUL, clause.Handler);
                    TraceValueNative(0xDFUL, clause.TargetType);
                    Boolean inRange = codeOffset >= clause.TryStart && codeOffset < clause.TryEnd;
                    Boolean typeMatch = clause.Kind == 0U && IsInstanceOf(exception, clause.TargetType);
                    TraceValueNative(0xE5UL, inRange ? 1UL : 0UL);
                    TraceValueNative(0xE6UL, typeMatch ? 1UL : 0UL);
                    if (clause.Kind == 0U && clause.Handler != skipHandler && inRange && typeMatch)
                    {
                        TraceNative(0xB7UL);
                        selectedContext = scan;
                        selectedHandler = clause.Handler;
                        return true;
                    }
                }
            }
            TraceNative(0xB8UL);
            Boolean unwound = VirtualUnwind(imageBase, fn, ref scan);
            TraceValueNative(0xE7UL, unwound ? 1UL : 0UL);
            if (unwound)
            {
                TraceValueNative(0xE8UL, scan.Ip);
                TraceValueNative(0xE9UL, scan.Sp);
                TraceValueNative(0xEAUL, scan.Rbp);
            }
            if (!unwound) break;
        }
        TraceNative(0xBEUL);
        return false;
    }

    private static void DispatchSameFrameRethrow(Object exception, EhContext* source, EhContext* selectedContext, UInt64 selectedHandler)
    {
        // The enclosing catch was already selected during pass 1 before the inner catch
        // funclet was entered. When `throw;` targets another catch in the same physical
        // frame there is no frame to unwind and no EH table to rediscover. Re-reading the
        // original EH table here is both redundant and unsafe because the source context
        // denotes the already-active protected region, not a fresh throw site. Transfer
        // directly to the cached enclosing clause, matching NativeAOT's ExInfo clause
        // progression for same-frame rethrow.
        EhContext target = *selectedContext;
        _activeCatchContext = target;
        _activeCatchHandler = selectedHandler;
        _activeCatchContextValid = true;

        // A further rethrow from the enclosing catch is resolved by the ordinary dispatcher
        // on its next throw path. Do not rescan the current source frame before entering the
        // cached catch; that was the deterministic A9-before-AA failure in 0.44.16.
        _activeRethrowContextValid = false;
        _activeRethrowHandler = 0UL;

        TraceNative(0xAAUL);
        UInt64 resumeIp = CallCatch(exception, selectedHandler, &target);
        if (resumeIp == 0UL) FailFast();
        TraceNative(0xABUL);
        Resume(resumeIp, &target);
        FailFast();
    }

    private static void DispatchSelected(Object exception, EhContext* source, EhContext* selectedContext, UInt64 selectedHandler)
    {
        EhContext cursor = *source;
        UInt64 imageBase = FindImageBase(cursor.Ip);
        if (imageBase == 0UL) FailFast();
        RuntimeFunction* table;
        UInt32 count;
        if (!GetExceptionDirectory(imageBase, out table, out count)) FailFast();

        // Pass 2: unwind and run active fault/finally funclets before the selected catch.
        EhContext targetContext = *selectedContext;
        EhContext scan = cursor;
        for (UInt32 depth = 0U; depth < 256U && scan.Ip != 0UL; depth++)
        {
            RuntimeFunction* fn = FindFunction(imageBase, table, count, scan.Ip);
            if (fn == null)
            {
                // Pass 2 may cross a pdata-less leaf only while the PC remains inside the
                // NativeAOT-managed range. Crossing Inu's native bootstrap boundary would
                // manufacture a caller from shadow-space data and corrupt exception dispatch.
                TraceNative(0xBAUL);
                if (!TryUnwindLeaf(imageBase, table, count, ref scan)) FailFast();
                continue;
            }
            RuntimeFunction* root = FindRootFunction(imageBase, table, fn);
            UInt64 methodStart = imageBase + root->Begin;
            UInt32 codeOffset = scan.Ip > methodStart ? (UInt32)(scan.Ip - methodStart - 1UL) : 0U;
            Byte* eh = GetEhInfo(imageBase, root);
            if (eh != null)
            {
                Byte* p = eh;
                UInt32 clauses = ReadUnsigned(ref p);
                for (UInt32 i = 0; i < clauses; i++)
                {
                    Clause clause = ReadClause(imageBase, methodStart, ref p);
                    if (clause.Kind == 1U && codeOffset >= clause.TryStart && codeOffset < clause.TryEnd)
                        CallFinally(clause.Handler, &scan);
                }
            }

            if (SameFrame(ref scan, ref targetContext))
            {
                _activeCatchContext = targetContext;
                _activeCatchHandler = selectedHandler;
                _activeCatchContextValid = true;

                // Resolve and retain the next enclosing catch while the original protected
                // region is still available. A later `throw;` can then continue directly to
                // this handler instead of attempting to rediscover scope from a funclet IP.
                EhContext nextContext;
                UInt64 nextHandler;
                EhContext rethrowSource = targetContext;
                _activeRethrowContextValid = TryFindCatch(exception, ref rethrowSource, imageBase, table, count,
                    selectedHandler, out nextContext, out nextHandler);
                _activeRethrowContext = nextContext;
                _activeRethrowHandler = nextHandler;

                TraceNative(0xA7UL);
                UInt64 resumeIp = CallCatch(exception, selectedHandler, &scan);
                if (resumeIp == 0UL) FailFast();
                Resume(resumeIp, &scan);
                FailFast();
            }

            if (!VirtualUnwind(imageBase, fn, ref scan)) FailFast();
        }
        FailFast();
    }

    private static Boolean SameFrame(ref EhContext a, ref EhContext b) => a.Rbp == b.Rbp && a.Sp == b.Sp && a.Ip == b.Ip;

    private static Boolean SamePhysicalFrame(ref EhContext a, ref EhContext b) => a.Rbp == b.Rbp && a.Sp == b.Sp;

    private static Boolean IsInstanceOf(Object value, UInt64 targetMethodTable)
    {
        // Typed catch selection must use the same NativeAOT MethodTable relation as
        // CoreLib casts/type tests. Keeping a second ancestry walker here diverged on
        // compiler/runtime-helper exception EETypes and stalled dispatch after EH:A5.
        return global::Inu.Runtime.RuntimeDiagnostics.IsExceptionInstanceOf(value, targetMethodTable);
    }

    private static UInt32 GetImageSize(UInt64 imageBase)
    {
        if (imageBase == 0UL || !IsPeImageHeader(imageBase)) return 0U;
        Byte* b = (Byte*)imageBase;
        UInt32 peOffset = *(UInt32*)(b + 0x3C);
        Byte* optional = b + peOffset + 24;
        UInt32 size = *(UInt32*)(optional + 56);
        return size >= 0x1000U ? size : 0U;
    }

    private static Boolean IsImageAddress(UInt64 imageBase, UInt32 imageSize, UInt64 address, UInt32 bytes)
    {
        if (imageBase == 0UL || imageSize == 0U || address < imageBase) return false;
        UInt64 end = imageBase + imageSize;
        UInt64 requestedEnd = address + bytes;
        if (end < imageBase || requestedEnd < address) return false;
        return requestedEnd <= end;
    }

    private static RuntimeFunction* FindRootFunction(UInt64 imageBase, RuntimeFunction* table, RuntimeFunction* fn)
    {
        RuntimeFunction* root = fn;
        for (UInt32 i = 0U; i < 256U && root > table; i++)
        {
            Byte flags = GetUnwindBlockFlags(imageBase, root);
            if ((flags & 0x03) == RootKind) break;
            root--;
        }
        return root;
    }

    private static UInt32 GetNativeAotFrameInfoOffset(Byte* unwindInfo)
    {
        // Match .NET 10.0.10 CoffCSharpodeManager::GetUnwindDataBlob exactly.
        // The JIT blob contains the standard x64 UNWIND_INFO and, when the Windows
        // EHANDLER/UHANDLER flags are present, the 4-byte personality-routine RVA.
        // NativeAOT's private FrameInfoFlags byte follows the complete JIT blob.
        Byte count = unwindInfo[2];
        Byte unwindFlags = (Byte)(unwindInfo[0] >> 3);
        UInt32 size = 4U + (UInt32)count * 2U;
        // NativeAOT places FrameInfoFlags immediately after the raw UNWIND_INFO when
        // there is no Windows personality routine. The 4-byte alignment exists only
        // to align the personality RVA when EHANDLER/UHANDLER is present. 0.0.195
        // aligned unconditionally, shifting FrameInfoFlags by two bytes whenever an
        // ordinary unwind record had an odd CountOfCodes and corrupting root/funclet
        // discovery during helper-thrown exception dispatch.
        if ((unwindFlags & (UnwFlagEHandler | UnwFlagUHandler)) != 0)
            size = ((size + 3U) & ~3U) + 4U;
        return size;
    }

    private static Byte GetUnwindBlockFlags(UInt64 imageBase, RuntimeFunction* fn)
    {
        Byte* u = (Byte*)(imageBase + fn->Unwind);
        return u[GetNativeAotFrameInfoOffset(u)];
    }

    private static Byte* GetEhInfo(UInt64 imageBase, RuntimeFunction* root)
    {
        Byte* u = (Byte*)(imageBase + root->Unwind);
        UInt32 frameInfoOffset = GetNativeAotFrameInfoOffset(u);
        Byte* p = u + frameInfoOffset;
        Byte blockFlags = *p++;

        // 0.0.94: raw xdata diagnostics. These values make the NativeAOT/COFF
        // contract visible in the serial trace instead of reducing every failure
        // to a null EH pointer.
        TraceValueNative(0xEBUL, root->Unwind);
        TraceValueNative(0xECUL, u[0]);
        TraceValueNative(0xEDUL, u[1]);
        TraceValueNative(0xEEUL, u[2]);
        TraceValueNative(0xEFUL, u[3]);
        TraceValueNative(0xF0UL, (Byte)(u[0] >> 3));
        TraceValueNative(0xF1UL, frameInfoOffset);
        TraceValueNative(0xF2UL, blockFlags);

        if ((blockFlags & HasAssociatedData) != 0)
        {
            TraceValueNative(0xF3UL, *(UInt32*)p);
            p += 4;
        }
        if ((blockFlags & HasEhInfo) == 0) return null;

        UInt32 rva = *(UInt32*)p;
        TraceValueNative(0xF4UL, rva);
        Byte* eh = (Byte*)(imageBase + rva);
        TraceValueNative(0xF5UL, (UInt64)(nuint)eh);
        return eh;
    }

    private static Clause ReadClause(UInt64 imageBase, UInt64 methodStart, ref Byte* p)
    {
        Clause c = default;
        c.TryStart = ReadUnsigned(ref p);
        UInt32 endKind = ReadUnsigned(ref p);
        c.Kind = endKind & 3U;
        c.TryEnd = c.TryStart + (endKind >> 2);
        if (c.Kind == 0U)
        {
            c.Handler = methodStart + ReadUnsigned(ref p);
            c.TargetType = imageBase + ReadUInt32(ref p);
        }
        else if (c.Kind == 1U)
        {
            c.Handler = methodStart + ReadUnsigned(ref p);
        }
        else if (c.Kind == 2U)
        {
            c.Handler = methodStart + ReadUnsigned(ref p);
            c.Filter = methodStart + ReadUnsigned(ref p);
        }
        return c;
    }

    private static UInt32 ReadUnsigned(ref Byte* p)
    {
        UInt32 val = *p;
        UInt32 value;
        if ((val & 1U) == 0U) { value = val >> 1; p += 1; }
        else if ((val & 2U) == 0U) { value = (val >> 2) | ((UInt32)p[1] << 6); p += 2; }
        else if ((val & 4U) == 0U) { value = (val >> 3) | ((UInt32)p[1] << 5) | ((UInt32)p[2] << 13); p += 3; }
        else if ((val & 8U) == 0U) { value = (val >> 4) | ((UInt32)p[1] << 4) | ((UInt32)p[2] << 12) | ((UInt32)p[3] << 20); p += 4; }
        else { value = (UInt32)p[1] | ((UInt32)p[2] << 8) | ((UInt32)p[3] << 16) | ((UInt32)p[4] << 24); p += 5; }
        return value;
    }

    private static UInt32 ReadUInt32(ref Byte* p)
    {
        UInt32 value = *(UInt32*)p;
        p += 4;
        return value;
    }

    private static Boolean GetExceptionDirectory(UInt64 imageBase, out RuntimeFunction* table, out UInt32 count)
    {
        table = null; count = 0U;
        Byte* b = (Byte*)imageBase;
        if (*(UInt16*)b != 0x5A4D) return false;
        UInt32 peOffset = *(UInt32*)(b + 0x3C);
        Byte* pe = b + peOffset;
        if (*(UInt32*)pe != 0x00004550U) return false;
        Byte* optional = pe + 24;
        if (*(UInt16*)optional != 0x20B) return false;
        UInt32 rva = *(UInt32*)(optional + 112 + 3 * 8);
        UInt32 size = *(UInt32*)(optional + 112 + 3 * 8 + 4);
        if (rva == 0U || size < 12U) return false;
        table = (RuntimeFunction*)(imageBase + rva);
        count = size / 12U;
        return count != 0U;
    }

    private static RuntimeFunction* FindFunction(UInt64 imageBase, RuntimeFunction* table, UInt32 count, UInt64 ip)
    {
        if (ip < imageBase || table == null || count == 0U) return null;
        UInt32 managedStart;
        UInt32 managedEnd;
        if (!GetManagedCodeRange(table, count, out managedStart, out managedEnd)) return null;

        UInt64 relative64 = ip - imageBase;
        if (relative64 > UInt32.MaxValue) return null;
        UInt32 relative = (UInt32)relative64;
        if (relative < managedStart || relative >= managedEnd) return null;

        // Match .NET NativeAOT's COFF code-manager lookup: select the RuntimeFunction
        // having the greatest BeginAddress not greater than the control PC. Do not use
        // EndAddress as a binary-search discriminator; return addresses can sit on a
        // function boundary and NativeAOT deliberately attributes those PCs by Begin.
        UInt32 low = 0U;
        UInt32 high = count;
        while (low + 1U < high)
        {
            UInt32 mid = low + ((high - low) >> 1);
            if ((table + mid)->Begin <= relative) low = mid;
            else high = mid;
        }
        return (table + low)->Begin <= relative ? table + low : null;
    }

    private static Boolean GetManagedCodeRange(RuntimeFunction* table, UInt32 count, out UInt32 start, out UInt32 end)
    {
        start = 0U;
        end = 0U;
        if (table == null || count == 0U) return false;

        UInt32 previousBegin = table->Begin;
        start = previousBegin;
        for (UInt32 i = 0U; i < count; i++)
        {
            RuntimeFunction* fn = table + i;
            if (fn->End < fn->Begin) return false;
            if (i != 0U && fn->Begin < previousBegin) return false;
            previousBegin = fn->Begin;
            if (fn->End > end) end = fn->End;
        }
        return end > start;
    }

    private static Boolean IsManagedCodeAddress(UInt64 imageBase, RuntimeFunction* table, UInt32 count, UInt64 ip)
    {
        if (ip < imageBase) return false;
        UInt32 start;
        UInt32 end;
        if (!GetManagedCodeRange(table, count, out start, out end)) return false;
        UInt64 relative = ip - imageBase;
        return relative >= start && relative < end;
    }

    private static UInt64 _resolvedImageBase;

    private static UInt64 FindImageBase(UInt64 ip)
    {
        // Inu's UEFI loader allocates the relocatable kernel with EFI page
        // granularity (4 KiB). A relocated PE image is therefore not guaranteed to
        // retain the PE preferred 64-KiB image alignment. 0.44.7 incorrectly
        // rounded the throw IP to 64 KiB and stepped by 64 KiB, which skipped valid
        // loader addresses such as 0x1DB4E000 entirely.
        UInt64 cached = _resolvedImageBase;
        if (cached != 0UL && IsPeImageContaining(cached, ip)) return cached;

        const UInt64 PageSize = 0x1000UL;
        UInt64 candidate = ip & ~(PageSize - 1UL);

        // The loader maps SizeOfImage as one contiguous allocation, so walking from
        // an executing instruction down toward the image base stays inside mapped
        // kernel pages until the MZ/PE header is reached. 32K pages gives a 128-MiB
        // upper bound without making assumptions about the current kernel size.
        for (UInt32 i = 0U; i < 32768U && candidate >= PageSize; i++)
        {
            if (IsPeImageContaining(candidate, ip))
            {
                _resolvedImageBase = candidate;
                return candidate;
            }
            candidate -= PageSize;
        }
        return 0UL;
    }

    private static Boolean IsPeImageHeader(UInt64 candidate)
    {
        Byte* b = (Byte*)candidate;
        if (*(UInt16*)b != 0x5A4D) return false;
        UInt32 peOffset = *(UInt32*)(b + 0x3C);
        if (peOffset < 0x40U || peOffset > 0x1000U) return false;
        Byte* pe = b + peOffset;
        return *(UInt32*)pe == 0x00004550U && *(UInt16*)(pe + 24) == 0x20BU;
    }

    private static Boolean IsPeImageContaining(UInt64 candidate, UInt64 ip)
    {
        if (!IsPeImageHeader(candidate)) return false;
        Byte* b = (Byte*)candidate;
        UInt32 peOffset = *(UInt32*)(b + 0x3C);
        Byte* optional = b + peOffset + 24;
        UInt32 sizeOfImage = *(UInt32*)(optional + 56);
        if (sizeOfImage < 0x1000U) return false;
        UInt64 end = candidate + sizeOfImage;
        if (end < candidate) return false;
        return ip >= candidate && ip < end;
    }

    private static Boolean VirtualUnwind(UInt64 imageBase, RuntimeFunction* fn, ref EhContext c)
    {
        Byte* u = (Byte*)(imageBase + fn->Unwind);
        Byte unwindFlags = (Byte)(u[0] >> 3);
        if ((unwindFlags & UnwFlagChainInfo) != 0) return false;
        Byte count = u[2];
        Byte frame = u[3];
        Byte frameReg = (Byte)(frame & 0x0F);
        Byte frameOff = (Byte)(frame >> 4);
        Byte* codes = u + 4;
        UInt32 i = 0U;
        while (i < count)
        {
            Byte op = (Byte)(codes[i * 2 + 1] & 0x0F);
            Byte info = (Byte)(codes[i * 2 + 1] >> 4);
            i++;
            if (op == 0) // UWOP_PUSH_NONVOL
            {
                SetRegister(ref c, info, *(UInt64*)c.Sp); c.Sp += 8UL;
            }
            else if (op == 1) // UWOP_ALLOC_LARGE
            {
                if (info == 0) { UInt16 slots = *(UInt16*)(codes + i * 2); i++; c.Sp += (UInt64)slots * 8UL; }
                else { UInt32 bytes = *(UInt32*)(codes + i * 2); i += 2; c.Sp += bytes; }
            }
            else if (op == 2) c.Sp += (UInt64)info * 8UL + 8UL; // UWOP_ALLOC_SMALL
            else if (op == 3) // UWOP_SET_FPREG
            {
                UInt64 fp = GetRegister(ref c, frameReg);
                c.Sp = fp - (UInt64)frameOff * 16UL;
            }
            else if (op == 4) // UWOP_SAVE_NONVOL
            {
                UInt16 slots = *(UInt16*)(codes + i * 2); i++;
                SetRegister(ref c, info, *(UInt64*)(c.Sp + (UInt64)slots * 8UL));
            }
            else if (op == 5) // UWOP_SAVE_NONVOL_FAR
            {
                UInt32 off = *(UInt32*)(codes + i * 2); i += 2;
                SetRegister(ref c, info, *(UInt64*)(c.Sp + off));
            }
            else if (op == 8) i++; // UWOP_SAVE_XMM128
            else if (op == 9) i += 2; // UWOP_SAVE_XMM128_FAR
            else if (op == 10) c.Sp += info == 0 ? 40UL : 48UL; // UWOP_PUSH_MACHFRAME
            else return false;
        }
        c.Ip = *(UInt64*)c.Sp;
        c.Sp += 8UL;
        return c.Ip != 0UL;
    }

    private static Boolean TryUnwindLeaf(UInt64 imageBase, RuntimeFunction* table, UInt32 count, ref EhContext c)
    {
        UInt32 imageSize = GetImageSize(imageBase);
        if (!IsManagedCodeAddress(imageBase, table, count, c.Ip)) return false;
        if (!IsImageAddress(imageBase, imageSize, c.Ip, 1U)) return false;
        if (c.Sp == 0UL || (c.Sp & 7UL) != 0UL) return false;

        // The x64 no-pdata leaf rule is valid only inside the code range owned by this
        // NativeAOT code manager. The caller is allowed to be the native host boundary;
        // the next pass-1 iteration will then stop instead of interpreting host shadow
        // space as another return address.
        UInt64 returnIp = *(UInt64*)c.Sp;
        if (returnIp == 0UL || !IsImageAddress(imageBase, imageSize, returnIp, 1U)) return false;
        c.Ip = returnIp;
        c.Sp += 8UL;
        return true;
    }

    private static UInt64 ReferenceAddress(Object value)
    {
        Object local = value;
        return System.Runtime.CompilerServices.Unsafe.As<Object, UInt64>(ref local);
    }

    private static UInt64 GetRegister(ref EhContext c, Byte reg)
    {
        if (reg == 3) return c.Rbx;
        if (reg == 5) return c.Rbp;
        if (reg == 6) return c.Rsi;
        if (reg == 7) return c.Rdi;
        if (reg == 12) return c.R12;
        if (reg == 13) return c.R13;
        if (reg == 14) return c.R14;
        if (reg == 15) return c.R15;
        return 0UL;
    }

    private static void SetRegister(ref EhContext c, Byte reg, UInt64 value)
    {
        if (reg == 3) c.Rbx = value;
        else if (reg == 5) c.Rbp = value;
        else if (reg == 6) c.Rsi = value;
        else if (reg == 7) c.Rdi = value;
        else if (reg == 12) c.R12 = value;
        else if (reg == 13) c.R13 = value;
        else if (reg == 14) c.R14 = value;
        else if (reg == 15) c.R15 = value;
    }

    private static void FailFast()
    {
        // Do not leave an unexplained silent spin if the freestanding EH contract fails.
        // Kath watches this allocation-free serial breadcrumb and terminates the run as a
        // runtime failure, returning the IDE's main control from Stop Run back to Run.
        TraceEmergencyNative(0x18FFUL);
        while (true) { }
    }
}
