# clone-disk
Linux Utility to clone disk reading parallel to write

## introduction

using dd to clone disk takes double of the time due to the fact it write data after each block read; with clone-disk instead  you can clone the disk in half time respect dd allowing each disk to run to maximum speed ( at the mean time at write speed of destination disk ).
It works by 8 slots of 64MB each used by reader and writer; at first reader fill 8 slots without blocking itself and writer start when first slot available to write down and when finished it signal reader to reuse slot consumed while writer wait again for any of reader slot become available.
Slots are filled and consumed sequentially and with each reader slot is associated a slot length to manage end of the disk chunks reading ( disk is not multiple of 64MB so last writing could smaller but sw also consider this case ).

## Quickstart

- Requirements: [Download NET Core SDK](https://dotnet.microsoft.com/download)
- Install the tool:

```sh
dotnet tool install -g clone-disk
```

- To update if already installed:

```sh
dotnet tool update -g clone-disk
```

- if `~/.dotnet/tools` dotnet global tool isn't in path it can be added to your `~/.bashrc`

```sh
echo 'export PATH=$PATH:~/.dotnet/tools' >> ~/.bashrc
```

*Note: disk must umounted*

## syntax

```
clone-disk <source> <dest>
```

> **Warning : double check source and destination device** the program take a change to interrupt at start prompt pressing `ctrl+c`

## example

copy contents of source disk /dev/sdb to target disk /dev/sdc

```
clone-disk /dev/sdb /dev/sdc
```

## source and destination identification

You can discover which disk are source and destination in either ways:

- watch at serial number `lsblk -o NAME,SERIAL,SIZE --nodeps` to identify which device is the source and which the destination

- alternatively, watch at `tail -f /var/log/syslog` to see which disk is source and destination by connecting disks after OS started. Connect source disk and write down device name then connect destination and do the same to fill program arguments correctly.

example of syslog ( note [sdc] name when disk attached )

```
[   30.831533] sd 2:0:0:0: [sdc] 3907029168 512-byte logical blocks: (2.00 TB/1.82 TiB)
[   30.831578] sd 2:0:0:0: [sdc] Write Protect is off
[   30.831582] sd 2:0:0:0: [sdc] Mode Sense: 00 3a 00 00
[   30.831637] sd 2:0:0:0: [sdc] Write cache: enabled, read cache: enabled, doesn't support DPO or FUA
[   30.845045] sd 2:0:0:0: [sdc] Attached SCSI disk
```

Note : to plug disk at runtime may you need to configure AHCI mode, ESATA or HotPlug feature.

## portability

- disk size are detected by querying `/sys/class/block/devicename/size`

## debugging

Use [vscode](https://code.visualstudio.com/)

Enter project folder and start vscode, then click on Restore popup to restore nuget packages
```
cd clone-disk
code .
```

Tune arguments in `.vscode/launch.json` ( "args" under "configurations" ) then Hit F5

You can test using loopback devices for a test purpose initializing these as follow ( replace XX, YY with free numbers watching at /dev/loop* files already allocated )

```
dd if=/dev/zero of=test1 bs=1M count=100
dd if=/dev/zero of=test2 bs=1M count=100
losetup -fP test1
losetup -fP test2
srcloop=$(losetup --list | grep `pwd`/test1 | awk '{print $1}')
dstloop=$(losetup --list | grep `pwd`/test2 | awk '{print $1}')
if [[ "$srcloop" == "" || "$dstloop" == "" ]]; then
  echo "couldn't find some loop dev"
else
  mkdir -p SRC
  mkfs.ext3 $srcloop
  mount $srcloop SRC
  ls /etc > SRC/file.txt
  umount SRC
  clone-disk --non-interactive $srcloop $dstloop
  mount $srcloop SRC
  mkdir -p DST
  mount $dstloop DST
  diff SRC/file.txt DST/file.txt
  if [ "$?" == "0" ]; then echo "tested successfully"; else echo "test ERROR"; fi
  umount SRC
  umount DST
  losetup -d $srcloop
  losetup -d $dstloop
  rm -f test1 test2  
fi
```

now you can safely test using /dev/loopXX /dev/loopYY arguments

## exitcodes

- 0 : ok
- 3 : can't fit source with destination device size

## execution test

```
root@bigone:/opt/clone-disk# /root/tmp-test-clone 
retrieving device size [/sys/class/block/sdb/size] = 3907029168 ( x 512 bytes blocks ) = 2000398934016 bytes = 1.8 Tb
retrieving device size [/sys/class/block/sdc/size] = 3907029168 ( x 512 bytes blocks ) = 2000398934016 bytes = 1.8 Tb
source disk = /dev/sdb size = 1.8 Tb
  dest disk = /dev/sdc
<===  read 17915904 bytes to bucket N. 0  read offset [1.8 Tb] speed =   87.8 Mb/s
===> write 17915904 bytes to bucket N. 0 write offset [1.8 Tb] speed =   87.8 Mb/s
*** FINISHED
```

## verification

For your own verification you can double check result by issueing and md5sum

```
dd if=/dev/sdb bs=512 count=$(cat /sys/class/block/<devicename>/size) | md5sum
```

example ( note : start in parallel from two terminals to avoid output mixing or redirect to a file )

```
$ dd if=/dev/sdb bs=512 count=$(cat /sys/class/block/sdb/size) | md5sum

3907029168+0 records in
3907029168+0 records out
2000398934016 bytes (2.0 TB, 1.8 TiB) copied, 20865.2 s, 95.9 MB/s
7d498cf3de1867f4c1a92fd00bb792a3  -

$ dd if=/dev/sdc bs=512 count=$(cat /sys/class/block/sdc/size) | md5sum

3907029168+0 records in
3907029168+0 records out
2000398934016 bytes (2.0 TB, 1.8 TiB) copied, 22016.3 s, 90.9 MB/s
7d498cf3de1867f4c1a92fd00bb792a3  -
```

## partition GUID

After disk cloned it has same content and same GUID partition number ; for a backup purpose you can leave backup disk the same but for some other reason you could need to change GUID partition ; to do that use following

```
sgdisk -G <device>
```
