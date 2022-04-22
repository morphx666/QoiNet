using System.Drawing;
using System.Drawing.Imaging;
using System.Net.NetworkInformation;

#pragma warning disable CA1416 // Validate platform compatibility
namespace Tester {
    internal class Program {
        public static void Main(string[] args) {
            string src = @"C:\Users\Xavier Flix\Dropbox\Projects\QoiNet\Release\qoi_test_images\";
            string trg = @"C:\Users\Xavier Flix\Dropbox\Projects\QoiNet\Release\qoi_test_images_decode\";

            // Qoi -> Png
            foreach(FileInfo file in new DirectoryInfo(src).GetFiles("*.qoi")) {
                Bitmap? bmp = QoiNet.QoiNet.FromQoiFile(file.FullName);
                bmp?.Save(trg + file.Name.Replace(file.Extension, "") + ".png", ImageFormat.Png);
            }

            // Png -> Qoi
            foreach(FileInfo file in new DirectoryInfo(src).GetFiles("*.png")) {
                string trgFile = trg + file.Name.Replace(file.Extension, "") + ".qoi";
                Bitmap bmp = (Bitmap)Bitmap.FromFile(file.FullName);
                QoiNet.QoiNet.ToQoiFile(bmp, trgFile);
            }
        }
    }
}