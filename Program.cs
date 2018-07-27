using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SearchAThing.NETCoreUtil;

namespace clone_disk
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name} <source> <dest>");
                System.Environment.Exit(1);
            }

            var sourceDevice = args[0];
            var destDevice = args[1];

            int bucketCount = 8;
            int bucketSize = 512 * 2 * 1024 * 64; // 64MB            
            byte[][] buckets = new byte[bucketCount][];
            int[] bucketLength = new int[bucketCount];
            for (int i = 0; i < bucketCount; ++i) buckets[i] = new byte[bucketSize];

            int readerNextBucket = 0;
            int readerNextWriteWaitHandle = 0;
            long readOffset = 0L;
            WaitHandle[] readWaitHandles = new WaitHandle[bucketCount];
            for (int i = 0; i < bucketCount; ++i) readWaitHandles[i] = new AutoResetEvent(true);

            int writerNextBucket = 0;
            int writerNextReadWaitHandle = 0;
            long writeOffset = 0L;
            WaitHandle[] writeWaitHandles = new WaitHandle[bucketCount];
            for (int i = 0; i < bucketCount; ++i) writeWaitHandles[i] = new AutoResetEvent(false);

            var sourceSize = GetDeviceSize(sourceDevice);
            var destSize = GetDeviceSize(destDevice);

            Console.WriteLine($"source disk = {sourceDevice} size = {sourceSize.HumanReadable()}");
            Console.WriteLine($"  dest disk = {destDevice}");

            if (destSize < sourceSize)
            {
                Console.WriteLine($"can't fit source into dest device");
                Environment.Exit(3);
            }

            var topCursor = 0;
            Console.Clear();

            var fsRead = File.OpenRead(sourceDevice);
            fsRead.Seek(0, SeekOrigin.Begin);

            var fsWrite = File.OpenWrite(destDevice);
            fsWrite.Seek(0, SeekOrigin.Begin);

            object guiLock = new object();

            var taskReader = Task.Run(() =>
            {
                DateTime dtReadStart = DateTime.Now;

                while (readOffset < sourceSize)
                {
                    readWaitHandles[readerNextBucket].WaitOne(); // initially already signaled

                    var len = fsRead.Read(buckets[readerNextBucket], 0, bucketSize);
                    bucketLength[readerNextBucket] = len;

                    long speed = (long)((double)(readOffset + len) / (DateTime.Now - dtReadStart).TotalSeconds);

                    lock (guiLock)
                    {
                        Console.SetCursorPosition(0, topCursor);
                        Console.Write($"<===  read {len} bytes to bucket N. {readerNextBucket}");
                        Console.Write($"  read offset [{readOffset.HumanReadable()}] speed = {speed.HumanReadable()}/s");                        
                    }

                    var writeEvt = (AutoResetEvent)writeWaitHandles[readerNextWriteWaitHandle];
                    readerNextWriteWaitHandle++;
                    writeEvt.Set();
                    if (readerNextWriteWaitHandle == bucketCount) readerNextWriteWaitHandle = 0;

                    readOffset += len;
                    readerNextBucket++;
                    if (readerNextBucket == bucketCount) readerNextBucket = 0;
                }
            });

            var taskWriter = Task.Run(() =>
            {
                DateTime dtWriteStart = DateTime.Now;

                while (writeOffset < sourceSize)
                {
                    writeWaitHandles[writerNextBucket].WaitOne();

                    var len = bucketLength[writerNextBucket];

                    fsWrite.Write(buckets[writerNextBucket], 0, len);

                    long speed = (long)((double)(writeOffset + len) / (DateTime.Now - dtWriteStart).TotalSeconds);

                    lock (guiLock)
                    {
                        Console.SetCursorPosition(0, topCursor + 1);
                        Console.Write($"===> write {len} bytes to bucket N. {writerNextBucket}");
                        Console.Write($" write offset [{writeOffset.HumanReadable()}] speed = {speed.HumanReadable()}/s");
                    }

                    var readEvt = (AutoResetEvent)readWaitHandles[writerNextReadWaitHandle];
                    writerNextReadWaitHandle++;
                    readEvt.Set();
                    if (writerNextReadWaitHandle == bucketCount) writerNextReadWaitHandle = 0;

                    writeOffset += len;
                    writerNextBucket++;
                    if (writerNextBucket == bucketCount) writerNextBucket = 0;
                }
            });

            Task.WaitAll(new Task[] { taskReader, taskWriter });

            Console.SetCursorPosition(0, topCursor + 2);
            Console.WriteLine("*** FINISHED ***");
        }

        static long GetDeviceSize(string device)
        {
            long res = 0L;
            long blocks = 0L;

            var devName = Path.GetFileName(device);

            var deviceSizeInfo = $"/sys/class/block/{devName}/size";
            Console.Write($"retrieving device size [{deviceSizeInfo}] = ");
            using (var sr = new StreamReader(deviceSizeInfo))
            {
                var line = sr.ReadLine();
                blocks = long.Parse(line);
            }
            res = blocks * 512;
            Console.WriteLine($"{blocks} ( x 512 bytes blocks ) = {res} bytes = {res.HumanReadable()}");

            return res;
        }
    }
}
