using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Readmusic
{
    class Program
    {
        static string URL = "https://edge01.daitsuna.net/orda/orda/icecast.audio";
        static int seconds = 20;

        static void Main()
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg/ffmpeg.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            int bitrate;
            int Byterate;
            Process process = null;

            void Startffmpeg()
            {
                bitrate = GetBitrate();

                Byterate = bitrate / 8;

                startInfo.Arguments = $"-i {URL} -ac 2 -vn -b:a {bitrate / 1000}k -f mp3 pipe:1";

                process = Process.Start(startInfo);
            }

            Startffmpeg();


            bool error = false;

            while (true)
            {
                FileStream filestream = null;

                if (error)
                {
                    Startffmpeg();
                    error = false;
                }


                long writedbytes = 0;

                try
                {
                    long totalbytes = Byterate * seconds;

                    Console.WriteLine("totalbits:" + totalbytes);

                    int bufferSize = 1024;

                    byte[] buffer = new byte[bufferSize];

                    filestream = CreateFileStream();

                    int abortStrike = 0;

                    while (true)
                    {
                        int bytestoread = (totalbytes >= bufferSize) ? bufferSize : (int)totalbytes;

                        int readBytes = 0;

                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

                        Thread thread = new Thread(() => 
                        {
                            try
                            {
                                var baseStream = process.StandardOutput.BaseStream;

                               readBytes = baseStream.ReadAsync(buffer, 0, bytestoread, cancellationTokenSource.Token).Result;

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        });

                        thread.Start();

                        if (!thread.Join(20000))
                        {
#if DEBUG
                            try
                            {
#endif
                                cancellationTokenSource.Cancel();
                                thread.Abort();
#if DEBUG
                            }
                            catch (Exception ex)
                            { }
#endif
                            Console.WriteLine("Abort");

                            abortStrike++;

                            if (abortStrike == 3)
                            {
                                throw new Exception("Cant read music");
                            }

                            continue;
                        }
                        else
                            abortStrike = 0;

                        totalbytes -= readBytes;
                        writedbytes += readBytes;
                        if (totalbytes <= 0)
                        {
                            break;
                        }
                        if (readBytes == 0)
                        {
                            Console.WriteLine("Error");

                            throw new Exception("Getting zero bytes");
                        }

                        filestream.Write(buffer, 0, readBytes);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    error = true;

                    if (process != null)
                    {
                        if (!process.HasExited)
                            process.Kill();

                        using (process.StandardOutput) { }
                        process.Close();
                    }

                }
                finally
                {
                    if (filestream != null)
                    using (filestream)
                    { }

                    Console.WriteLine("writedbytes: " + writedbytes);

                }
            }

        }

        public static FileStream CreateFileStream()
        {
            DateTime dateTime = DateTime.Now;

            var directoryName = $"records/records{string.Join(' ', dateTime.Day, dateTime.Month, dateTime.Year)}";

            Directory.CreateDirectory(directoryName);

            string fullPath = $"{directoryName}/record{string.Join(' ', dateTime.Hour, dateTime.Minute, dateTime.Second)}.mp3";

            Console.WriteLine($"Write to {fullPath}");

            return File.Open(fullPath, FileMode.Create, FileAccess.ReadWrite);
        }

        public static int GetBitrate()
        {
            int outbitrate;

            var startInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg/ffprobe.exe",
                Arguments = $"-show_entries stream=bit_rate -of compact=p=0:nk=1 {URL}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            var process = Process.Start(startInfo);

            var reader = process.StandardOutput;

            while (true)
            {
                if (int.TryParse(reader.ReadLine(), out outbitrate))
                    break;

                if (!process.HasExited)
                    process.Kill();
                process.Close();
                process = Process.Start(startInfo);
                reader = process.StandardOutput;
                Thread.Sleep(10000);
                Console.WriteLine("Waiting for network");
            }

            if (!process.HasExited)
                process.Kill();
            process.Close();

            return outbitrate;
        }

    }
}
