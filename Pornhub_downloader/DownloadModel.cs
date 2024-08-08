using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pornhub_downloader
{
    public class DownloadModel
    {
        public static string guid { get; private set; } = Guid.NewGuid().ToString();
        public ConcurrentDictionary<int, byte[]> TargetVideo = new ConcurrentDictionary<int, byte[]>();
        private Task[]? tasks { get; set; }
        private int packageCnt { get; set;}
        private ConcurrentQueue<int> MsgConsoleQueue = new ConcurrentQueue<int>();
        private bool finishFlag = false;

        /// <summary>
        /// download all ts file
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ts"></param>
        /// <param name="baseUrl"></param>
        /// <returns></returns>
        public static async Task downloadTs(HttpClient client, List<string> ts, string baseUrl)
        {
            using (FileStream fs = new FileStream($"{guid}.mp4", FileMode.OpenOrCreate, FileAccess.Write))
            {
                var index = 0;
                var len = ts.Count;
                foreach (string s in ts)
                {
                    byte[] tmp = await client.GetByteArrayAsync(baseUrl + s);

                    fs.Write(tmp, 0, tmp.Length);

                    process_show(++index, len);
                }
            }
        }
        private static void process_show(int index, int cnt)
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
            double percentage = (double)index / cnt * 100;
            Console.Write($"Downloading ... [{Math.Round(percentage, 2)}%]");
        }
        /// <summary>
        /// multithreading download all ts file
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ts"></param>
        /// <param name="baseUrl"></param>
        /// <param name="thread_count"></param>
        /// <returns></returns>
        public async Task downloadTsAsync(HttpClient client, List<string> ts, string baseUrl, int thread_count = 4)
        {
            packageCnt = ts.Count;
            tasks = new Task[packageCnt];
            SemaphoreSlim semaphore = new SemaphoreSlim(thread_count);

            // 守护线程
            Task.Run(() =>
            {
                while (!finishFlag)
                {
                    while (MsgConsoleQueue.TryDequeue(out _)) process_show(TargetVideo.Count, packageCnt);
                }
            });

            for (int index = 0; index < packageCnt; index++)
                tasks[index] = downloadChunkAsync(client, ts[index], baseUrl, semaphore, index);

            await Task.WhenAll(tasks);
            finishFlag = true;

            using (FileStream fs = new FileStream($"{guid}.mp4", FileMode.OpenOrCreate, FileAccess.Write))
            {
                for(int index = 0; index < TargetVideo.Keys.Count; index++)
                    fs.Write(TargetVideo[index], 0, TargetVideo[index].Length);
            }
        }
        private async Task downloadChunkAsync(HttpClient client, string target, string baseUrl, SemaphoreSlim semaphore, int index)
        {
            await semaphore.WaitAsync();

            try
            {
                byte[] tmp = await client.GetByteArrayAsync(baseUrl + target);
                TargetVideo[index] = tmp;
            }
            catch (Exception ex)
            {
                Console.WriteLine("error network!stopping");
                Environment.Exit(-1);
            }
            finally
            {
                MsgConsoleQueue.Enqueue(1);
                semaphore.Release();
            }


        }
    }
}
