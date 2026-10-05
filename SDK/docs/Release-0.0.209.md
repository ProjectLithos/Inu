# Inu 0.0.209

## FAT mkdir performance repair

- Removes the 0.0.208 `mkdir` 30-second special timeout; `mkdir` returns to the ordinary command timeout and no longer prints the misleading long-running-command banner merely because of package policy.
- Replaces cluster allocation's one-FAT-entry-per-I/O scan with sector-at-a-time free-cluster scanning for FAT16/FAT32. This removes hundreds or thousands of synchronous block reads from the common allocation path.
- Adds a per-mount next-allocation hint so subsequent file/directory allocations start after the last successful cluster instead of rescanning from cluster 2.
- Removes the redundant second whole-cluster zero performed while initialising a newly allocated directory; `AllocateCluster` already zeroes the cluster, so directory initialisation now writes only the first sector containing `.` and `..`.
- Keeps the working VFAT long-filename create/read/remove behaviour from 0.0.207/0.0.208.

The intended runtime result is that both short and VFAT long-name `mkdir` operations complete promptly instead of being treated as inherently long-running operations.

## GUI status

Desktop/Login remain materialised userland source. Independent ring-3 application build/autostart is still the next userland milestone and is not falsely claimed complete by this performance release.
