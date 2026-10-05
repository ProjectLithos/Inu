/* Inu x64 UEFI loader.
 *
 * This is deliberately the only EFI application in a Inu boot image.
 * It loads the non-EFI kernel from \INU\KERNEL\KERNEL.BIN and the
 * partition-authored system asset bundle from \INU\SYSTEM\ASSETS.BIN.
 * The kernel remains responsible for graphics/ACPI capture and ExitBootServices.
 */

typedef unsigned char      UINT8;
typedef unsigned short     UINT16;
typedef unsigned int       UINT32;
typedef unsigned long long UINT64;
typedef UINT64             UINTN;
typedef UINT64             EFI_STATUS;
typedef void*              EFI_HANDLE;
typedef UINT16             CHAR16;

typedef struct { UINT32 Data1; UINT16 Data2; UINT16 Data3; UINT8 Data4[8]; } EFI_GUID;
typedef struct { UINT64 Signature; UINT32 Revision; UINT32 HeaderSize; UINT32 Crc32; UINT32 Reserved; } EFI_TABLE_HEADER;

typedef EFI_STATUS (*EFI_ALLOCATE_PAGES)(UINT32, UINT32, UINTN, UINT64*);
typedef EFI_STATUS (*EFI_FREE_PAGES)(UINT64, UINTN);
typedef EFI_STATUS (*EFI_ALLOCATE_POOL)(UINT32, UINTN, void**);
typedef EFI_STATUS (*EFI_FREE_POOL)(void*);
typedef EFI_STATUS (*EFI_HANDLE_PROTOCOL)(EFI_HANDLE, EFI_GUID*, void**);

typedef struct EFI_BOOT_SERVICES {
    EFI_TABLE_HEADER Hdr;
    void* RaiseTPL;
    void* RestoreTPL;
    EFI_ALLOCATE_PAGES AllocatePages;
    EFI_FREE_PAGES FreePages;
    void* GetMemoryMap;
    EFI_ALLOCATE_POOL AllocatePool;
    EFI_FREE_POOL FreePool;
    void* CreateEvent;
    void* SetTimer;
    void* WaitForEvent;
    void* SignalEvent;
    void* CloseEvent;
    void* CheckEvent;
    void* InstallProtocolInterface;
    void* ReinstallProtocolInterface;
    void* UninstallProtocolInterface;
    EFI_HANDLE_PROTOCOL HandleProtocol;
} EFI_BOOT_SERVICES;

typedef struct EFI_SYSTEM_TABLE {
    EFI_TABLE_HEADER Hdr;
    CHAR16* FirmwareVendor;
    UINT32 FirmwareRevision;
    UINT32 Padding;
    EFI_HANDLE ConsoleInHandle;
    void* ConIn;
    EFI_HANDLE ConsoleOutHandle;
    void* ConOut;
    EFI_HANDLE StandardErrorHandle;
    void* StdErr;
    void* RuntimeServices;
    EFI_BOOT_SERVICES* BootServices;
    UINTN NumberOfTableEntries;
    void* ConfigurationTable;
} EFI_SYSTEM_TABLE;

typedef struct EFI_LOADED_IMAGE_PROTOCOL {
    UINT32 Revision;
    UINT32 Padding;
    EFI_HANDLE ParentHandle;
    EFI_SYSTEM_TABLE* SystemTable;
    EFI_HANDLE DeviceHandle;
    void* FilePath;
    void* Reserved;
    UINT32 LoadOptionsSize;
    UINT32 Padding2;
    void* LoadOptions;
    void* ImageBase;
    UINT64 ImageSize;
    UINT32 ImageCodeType;
    UINT32 ImageDataType;
    EFI_STATUS (*Unload)(EFI_HANDLE);
} EFI_LOADED_IMAGE_PROTOCOL;

struct EFI_FILE_PROTOCOL;
typedef EFI_STATUS (*EFI_FILE_OPEN)(struct EFI_FILE_PROTOCOL*, struct EFI_FILE_PROTOCOL**, CHAR16*, UINT64, UINT64);
typedef EFI_STATUS (*EFI_FILE_CLOSE)(struct EFI_FILE_PROTOCOL*);
typedef EFI_STATUS (*EFI_FILE_READ)(struct EFI_FILE_PROTOCOL*, UINTN*, void*);
typedef EFI_STATUS (*EFI_FILE_GET_INFO)(struct EFI_FILE_PROTOCOL*, EFI_GUID*, UINTN*, void*);
typedef struct EFI_FILE_PROTOCOL {
    UINT64 Revision;
    EFI_FILE_OPEN Open;
    EFI_FILE_CLOSE Close;
    void* Delete;
    EFI_FILE_READ Read;
    void* Write;
    void* GetPosition;
    void* SetPosition;
    EFI_FILE_GET_INFO GetInfo;
} EFI_FILE_PROTOCOL;

typedef struct EFI_SIMPLE_FILE_SYSTEM_PROTOCOL {
    UINT64 Revision;
    EFI_STATUS (*OpenVolume)(struct EFI_SIMPLE_FILE_SYSTEM_PROTOCOL*, EFI_FILE_PROTOCOL**);
} EFI_SIMPLE_FILE_SYSTEM_PROTOCOL;

typedef struct { UINT64 Size; UINT64 FileSize; UINT64 PhysicalSize; } EFI_FILE_INFO_PREFIX;

typedef EFI_STATUS (*INU_KERNEL_ENTRY)(EFI_HANDLE, EFI_SYSTEM_TABLE*, void*, UINT64, UINT64);

#define EFI_SUCCESS 0ULL
#define EFI_LOAD_ERROR 0x8000000000000001ULL
#define EFI_NOT_FOUND 0x800000000000000EULL
#define EFI_FILE_MODE_READ 1ULL
#define AllocateAnyPages 0U
#define AllocateAddress 2U
#define EfiLoaderCode 1U
#define EfiLoaderData 2U
#define PAGE_SIZE 4096ULL
#define IMAGE_REL_BASED_ABSOLUTE 0U
#define IMAGE_REL_BASED_DIR64 10U

static EFI_GUID LoadedImageGuid = {0x5B1B31A1U,0x9562U,0x11D2U,{0x8E,0x3F,0x00,0xA0,0xC9,0x69,0x72,0x3B}};
static EFI_GUID SimpleFileSystemGuid = {0x964E5B22U,0x6459U,0x11D2U,{0x8E,0x39,0x00,0xA0,0xC9,0x69,0x72,0x3B}};
static EFI_GUID FileInfoGuid = {0x09576E92U,0x6D3FU,0x11D2U,{0x8E,0x39,0x00,0xA0,0xC9,0x69,0x72,0x3B}};

/* Keep one absolute image-local pointer in the EFI loader. The loader is otherwise
   entirely RIP-relative, which lets lld-link omit the PE base-relocation directory.
   Firmware is free to place BOOTX64.EFI away from its preferred PE image base, so
   a relocation directory must always exist. This live volatile anchor deliberately
   forces a DIR64 relocation and keeps the first-stage loader location-independent. */
static void* volatile InuBootLoaderRelocationAnchor = (void*)&LoadedImageGuid;

static CHAR16 KernelPath[] = {'\\','I','N','U','\\','K','E','R','N','E','L','\\','K','E','R','N','E','L','.','B','I','N',0};
static CHAR16 AssetsPath[] = {'\\','I','N','U','\\','S','Y','S','T','E','M','\\','A','S','S','E','T','S','.','B','I','N',0};

static UINT16 Read16(const UINT8* p) { return (UINT16)((UINT16)p[0] | ((UINT16)p[1] << 8)); }
static UINT32 Read32(const UINT8* p) { return (UINT32)p[0] | ((UINT32)p[1]<<8) | ((UINT32)p[2]<<16) | ((UINT32)p[3]<<24); }
static UINT64 Read64(const UINT8* p) { return (UINT64)Read32(p) | ((UINT64)Read32(p+4)<<32); }
static void CopyBytes(UINT8* d,const UINT8* s,UINT64 n) { while(n-- != 0ULL) *d++=*s++; }
static void ZeroBytes(UINT8* d,UINT64 n) { while(n-- != 0ULL) *d++=0; }

static EFI_STATUS ReadFile(EFI_BOOT_SERVICES* bs, EFI_FILE_PROTOCOL* root, CHAR16* path, void** buffer, UINT64* length)
{
    EFI_FILE_PROTOCOL* file=0;
    void* info=0;
    void* data=0;
    UINTN infoSize=0;
    UINTN readSize=0;
    EFI_STATUS status;
    if(!bs||!root||!path||!buffer||!length) return EFI_LOAD_ERROR;
    *buffer=0;*length=0;
    status=root->Open(root,&file,path,EFI_FILE_MODE_READ,0);
    if(status!=EFI_SUCCESS||!file) return status?status:EFI_NOT_FOUND;
    status=file->GetInfo(file,&FileInfoGuid,&infoSize,0);
    if(infoSize<sizeof(EFI_FILE_INFO_PREFIX)) { file->Close(file); return status?status:EFI_LOAD_ERROR; }
    status=bs->AllocatePool(EfiLoaderData,infoSize,&info);
    if(status!=EFI_SUCCESS||!info) { file->Close(file); return status?status:EFI_LOAD_ERROR; }
    status=file->GetInfo(file,&FileInfoGuid,&infoSize,info);
    if(status!=EFI_SUCCESS) { bs->FreePool(info); file->Close(file); return status; }
    UINT64 fileSize=((EFI_FILE_INFO_PREFIX*)info)->FileSize;
    bs->FreePool(info);
    if(fileSize==0ULL) { file->Close(file); return EFI_LOAD_ERROR; }
    status=bs->AllocatePool(EfiLoaderData,(UINTN)fileSize,&data);
    if(status!=EFI_SUCCESS||!data) { file->Close(file); return status?status:EFI_LOAD_ERROR; }
    readSize=(UINTN)fileSize;
    status=file->Read(file,&readSize,data);
    file->Close(file);
    if(status!=EFI_SUCCESS||readSize!=(UINTN)fileSize) { bs->FreePool(data); return status?status:EFI_LOAD_ERROR; }
    *buffer=data;*length=fileSize;return EFI_SUCCESS;
}

static UINT8 PortRead8(UINT16 port) { UINT8 value; __asm__ volatile("inb %1, %0" : "=a"(value) : "Nd"(port)); return value; }
static void PortWrite8(UINT16 port, UINT8 value) { __asm__ volatile("outb %0, %1" :: "a"(value), "Nd"(port)); }
static int InuEarlySerialReady = 0;
static void EarlySerialInitialize(void)
{
    if(InuEarlySerialReady) return;
    PortWrite8(0x3F9U,0x00U); /* disable interrupts */
    PortWrite8(0x3FBU,0x80U); /* DLAB */
    PortWrite8(0x3F8U,0x01U); /* 115200 baud divisor */
    PortWrite8(0x3F9U,0x00U);
    PortWrite8(0x3FBU,0x03U); /* 8N1 */
    PortWrite8(0x3FAU,0xC7U); /* FIFO */
    PortWrite8(0x3FCU,0x0BU); /* IRQs/out2/rts/dtr */
    InuEarlySerialReady=1;
}
static void EarlySerialByte(UINT8 value)
{
    EarlySerialInitialize();
    for(UINT32 spin=0U;spin<1000000U;spin++) if((PortRead8(0x3FDU)&0x20U)!=0U){PortWrite8(0x3F8U,value);return;}
}
static void DebugByte(UINT8 value) { __asm__ volatile("outb %0, $0xe9" :: "a"(value)); EarlySerialByte(value); }
static void DebugText(const char* text) { if(!text) return; while(*text) DebugByte((UINT8)*text++); }
static void DebugHexNibble(UINT8 value) { DebugByte((UINT8)(value<10U?('0'+value):('A'+(value-10U)))); }
static void DebugHex64(UINT64 value) { for(int shift=60;shift>=0;shift-=4) DebugHexNibble((UINT8)((value>>(UINT32)shift)&0xFULL)); }
static EFI_STATUS DebugFail(const char* stage, EFI_STATUS status)
{
    DebugText("NOBLDR:FAIL:"); DebugText(stage); DebugByte(':'); DebugHex64(status); DebugByte('\n');
    return status?status:EFI_LOAD_ERROR;
}

static EFI_STATUS ApplyBaseRelocations(UINT8* image, UINT32 sizeOfImage, UINT32 relocationRva, UINT32 relocationSize, UINT64 delta)
{
    if(delta==0ULL) return EFI_SUCCESS;
    if(!image||relocationRva==0U||relocationSize<8U||(UINT64)relocationRva+(UINT64)relocationSize>(UINT64)sizeOfImage) return EFI_LOAD_ERROR;
    UINT32 consumed=0U;
    while(consumed<relocationSize)
    {
        if(relocationSize-consumed<8U) return EFI_LOAD_ERROR;
        UINT8* block=image+relocationRva+consumed;
        UINT32 pageRva=Read32(block), blockSize=Read32(block+4);
        if(blockSize<8U||blockSize>relocationSize-consumed||((blockSize-8U)&1U)!=0U) return EFI_LOAD_ERROR;
        UINT32 count=(blockSize-8U)/2U;
        for(UINT32 i=0;i<count;i++)
        {
            UINT16 fix=Read16(block+8U+i*2U);
            UINT16 type=(UINT16)(fix>>12), offset=(UINT16)(fix&0x0FFFU);
            if(type==IMAGE_REL_BASED_ABSOLUTE) continue;
            if(type!=IMAGE_REL_BASED_DIR64) return EFI_LOAD_ERROR;
            UINT64 targetRva=(UINT64)pageRva+(UINT64)offset;
            if(targetRva+8ULL>(UINT64)sizeOfImage) return EFI_LOAD_ERROR;
            UINT8* target=image+targetRva;
            UINT64 value=Read64(target)+delta;
            for(UINT32 b=0;b<8U;b++) target[b]=(UINT8)(value>>(b*8U));
        }
        consumed+=blockSize;
    }
    return consumed==relocationSize?EFI_SUCCESS:EFI_LOAD_ERROR;
}

static EFI_STATUS LoadRelocatableKernel(EFI_BOOT_SERVICES* bs, void* fileBuffer, UINT64 fileLength, INU_KERNEL_ENTRY* entry, UINT64* loadedBase)
{
    UINT8* file=(UINT8*)fileBuffer;
    if(!bs||!file||!entry||!loadedBase||fileLength<0x200ULL) return EFI_LOAD_ERROR;
    if(Read16(file)!=0x5A4DU) return EFI_LOAD_ERROR;
    UINT32 peOffset=Read32(file+0x3C);
    if((UINT64)peOffset+24ULL>fileLength) return EFI_LOAD_ERROR;
    UINT8* pe=file+peOffset;
    if(Read32(pe)!=0x00004550U||Read16(pe+4)!=0x8664U) return EFI_LOAD_ERROR;
    UINT16 sectionCount=Read16(pe+6), optionalSize=Read16(pe+20);
    UINT8* optional=pe+24;
    if((UINT64)(optional-file)+optionalSize>fileLength||optionalSize<0xA0U||Read16(optional)!=0x20BU) return EFI_LOAD_ERROR;
    UINT32 entryRva=Read32(optional+16);
    UINT64 imageBase=Read64(optional+24);
    UINT32 sizeOfImage=Read32(optional+56), sizeOfHeaders=Read32(optional+60);
    UINT32 directoryCount=Read32(optional+108);
    UINT32 relocationRva=0U, relocationSize=0U;
    if(directoryCount>5U&&optionalSize>=160U){ relocationRva=Read32(optional+152); relocationSize=Read32(optional+156); }
    if(imageBase==0ULL||sizeOfImage==0U||entryRva>=sizeOfImage) return EFI_LOAD_ERROR;
    UINT64 sectionTableOffset=(UINT64)(optional-file)+optionalSize;
    if(sectionTableOffset+(UINT64)sectionCount*40ULL>fileLength) return EFI_LOAD_ERROR;

    UINTN pages=((UINTN)sizeOfImage+PAGE_SIZE-1ULL)/PAGE_SIZE;
    UINT64 base=imageBase;
    EFI_STATUS status=bs->AllocatePages(AllocateAddress,EfiLoaderCode,pages,&base);
    if(status!=EFI_SUCCESS||base!=imageBase)
    {
        base=0ULL;
        status=bs->AllocatePages(AllocateAnyPages,EfiLoaderCode,pages,&base);
        if(status!=EFI_SUCCESS||base==0ULL) return status?status:EFI_LOAD_ERROR;
    }
    ZeroBytes((UINT8*)(UINTN)base,(UINT64)pages*PAGE_SIZE);
    UINT64 headerBytes=sizeOfHeaders;
    if(headerBytes>fileLength) headerBytes=fileLength;
    if(headerBytes>sizeOfImage) headerBytes=sizeOfImage;
    CopyBytes((UINT8*)(UINTN)base,file,headerBytes);
    UINT8* section=file+sectionTableOffset;
    for(UINT16 i=0;i<sectionCount;i++,section+=40)
    {
        UINT32 virtualSize=Read32(section+8), virtualAddress=Read32(section+12);
        UINT32 rawSize=Read32(section+16), rawOffset=Read32(section+20);
        UINT64 mappedSize=virtualSize>rawSize?virtualSize:rawSize;
        if((UINT64)virtualAddress+mappedSize>(UINT64)sizeOfImage) { bs->FreePages(base,pages); return EFI_LOAD_ERROR; }
        if(rawSize!=0U)
        {
            if((UINT64)rawOffset+(UINT64)rawSize>fileLength) { bs->FreePages(base,pages); return EFI_LOAD_ERROR; }
            CopyBytes((UINT8*)(UINTN)(base+virtualAddress),file+rawOffset,rawSize);
        }
    }
    if(base!=imageBase)
    {
        UINT64 delta=base-imageBase;
        status=ApplyBaseRelocations((UINT8*)(UINTN)base,sizeOfImage,relocationRva,relocationSize,delta);
        if(status!=EFI_SUCCESS) { bs->FreePages(base,pages); return status; }
    }
    DebugText("NOBLDR:KBASE:"); DebugHex64(base); DebugByte('\n');
    *entry=(INU_KERNEL_ENTRY)(UINTN)(base+entryRva);
    *loadedBase=base;
    return EFI_SUCCESS;
}

EFI_STATUS InuBootLoaderEntry(EFI_HANDLE imageHandle, EFI_SYSTEM_TABLE* systemTable)
{
    DebugText("NOBLDR:ENTRY\n");
    if(!imageHandle||!systemTable||!systemTable->BootServices||!InuBootLoaderRelocationAnchor) return DebugFail("ARGS",EFI_LOAD_ERROR);
    EFI_BOOT_SERVICES* bs=systemTable->BootServices;
    EFI_LOADED_IMAGE_PROTOCOL* loaded=0;
    EFI_SIMPLE_FILE_SYSTEM_PROTOCOL* fs=0;
    EFI_FILE_PROTOCOL* root=0;
    void* kernelFile=0;
    void* assets=0;
    UINT64 kernelLength=0, assetsLength=0;
    INU_KERNEL_ENTRY kernelEntry=0;
    UINT64 kernelBase=0;
    EFI_STATUS status=bs->HandleProtocol(imageHandle,&LoadedImageGuid,(void**)&loaded);
    if(status!=EFI_SUCCESS||!loaded||!loaded->DeviceHandle) return DebugFail("IMAGE",status);
    DebugText("NOBLDR:IMAGE\n");
    status=bs->HandleProtocol(loaded->DeviceHandle,&SimpleFileSystemGuid,(void**)&fs);
    if(status!=EFI_SUCCESS||!fs) return DebugFail("FS",status);
    DebugText("NOBLDR:FS\n");
    status=fs->OpenVolume(fs,&root);
    if(status!=EFI_SUCCESS||!root) return DebugFail("ROOT",status);
    status=ReadFile(bs,root,KernelPath,&kernelFile,&kernelLength);
    if(status!=EFI_SUCCESS) { root->Close(root); return DebugFail("KFILE",status); }
    DebugText("NOBLDR:KFILE\n");
    status=ReadFile(bs,root,AssetsPath,&assets,&assetsLength);
    root->Close(root);
    if(status!=EFI_SUCCESS) { bs->FreePool(kernelFile); return DebugFail("ASSET",status); }
    DebugText("NOBLDR:ASSET\n");
    status=LoadRelocatableKernel(bs,kernelFile,kernelLength,&kernelEntry,&kernelBase);
    bs->FreePool(kernelFile);
    if(status!=EFI_SUCCESS||!kernelEntry) { bs->FreePool(assets); return DebugFail("KMAP",status); }
    DebugText("NOBLDR:JUMP\n");
    /* assets intentionally remain allocated as EfiLoaderData. The kernel's physical-memory
       bootstrap does not reclaim loader allocations, so the partition-authored catalogue
       remains valid after ExitBootServices. */
    return kernelEntry(imageHandle,systemTable,assets,assetsLength,kernelBase);
}
