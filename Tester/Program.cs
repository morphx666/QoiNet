using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Tester {
    internal class Program {
        public static void Main(string[] args) {
            Console.WriteLine("QoiNet " + Assembly.GetEntryAssembly()?.GetName().Version + "\n");

            if(args.Length == 0) {
                Console.WriteLine("\tPNG->QOI:  Tester file.png");
                Console.WriteLine("\tQOI->PNG:  Tester file.qoi");
                Console.WriteLine("\tBenchmark: Tester <path>");
                Console.WriteLine("\t           Where path points to a folder containing PNGs and or QOIs\n");
            } else {
                FileInfo fi = new(args[0]);
                if(fi.Extension == "") {
                    RunBenchmark(args[0]);
                } else {
                    if(fi.Extension == ".qoi") {
                        Console.WriteLine($"Generated: {ToPng(fi)}\n");
                    } else {
                        Console.WriteLine($"Generated: {ToQoi(fi)}\n");
                    }
                }
            }
        }

        private static void RunBenchmark(string path) {
            DirectoryInfo di = new(path);
            FileInfo[] files = di.GetFiles();
            int maxLen = files.Max(f => f.Name.Length) + 4;
            Stopwatch sw = new();
            for(int i = 0; i < files.Length; i++) {
                string pad = new(' ', maxLen - files[i].Name.Length);
                sw.Restart();
                if(files[i].Extension == ".qoi") {
                    Console.WriteLine($"Generated: {ToPng(files[i])}{pad} | {files[i].Length / 1024.0,9:N2} KiB | {sw.ElapsedMilliseconds,4:N0} ms");
                } else {
                    Console.WriteLine($"Generated: {ToQoi(files[i])}{pad} | {files[i].Length / 1024.0,9:N2} KiB | {sw.ElapsedMilliseconds,4:N0} ms");
                }
            }
        }

        private static string ToQoi(FileInfo file) {
            string target = file.Name.Replace(file.Extension, ".qoi");
            Bitmap bmp = (Bitmap)Image.FromFile(file.FullName);
            QoiNet.QoiNet.ToQoiFile(bmp, target);
            return target;
        }

        private static string ToPng(FileInfo file) {
            string target = file.Name.Replace(file.Extension, ".png");
            Bitmap? bmp = QoiNet.QoiNet.FromQoiFile(file.FullName);
            bmp?.Save(target, ImageFormat.Png);
            return target;
        }
    }
}