using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing; 
using System.Windows.Media.Imaging;

namespace DumpJPGs
{
    class Program
    {
        public static string inFolder, outFolder;

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("You must specify an input folder (preview root) and output folder (to dump jpg's) on the command line.");
                return;
            }
            inFolder = args[0];
            if (!Directory.Exists(inFolder))
            {
                Console.WriteLine("The first parameter should be the preview folder root (e.g. \"C:\\stuff\\stuff Previews.lrdata\" and should be in double quotes).");
                return;
            }
            outFolder = args[1];
            if (!Directory.Exists(inFolder))
            {
                Console.WriteLine("The second parameter should be the output folder and must already exist (e.g. \"C:\\jpg out\" and in double quotes if there are spaces or punctuation.");
                return;
            }
            DirSearch(inFolder); 
        }
        static void DirSearch(string sDir)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        if (Path.GetExtension(f) == ".lrprev")
                        {
                            GetPreview(f, Path.Combine(outFolder, File.GetCreationTime(f).ToString("yyyyMMdd-hhmmss-") + Path.GetFileNameWithoutExtension(f)) + ".jpg");
                        }
                    }
                    DirSearch(d);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine("Exception in DirSearch={0}",excpt.Message);
            }
        }

        public static void GetPreview(string cacheFilePath, string outFilePath /*, string orientation */)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(cacheFilePath);

                // Find all JPG's inside of image by looking for boundaries of 0xFFD8 and 0xFFD9   
                int[,] startLen = new int[10, 4];  // 10 = more than we need, 0-4 as below
                const int Startpos = 0;
                const int Lenpos = 1;
                const int Heightpos = 2;
                const int Widthpos = 3;

                int holdstart = -1;
                int holdheight = -1;
                int holdwidth = -1;
                int lastpositionsize = -1;
                int lastpositionstart = -1;
                int maxSizeSoFar = -1;
                int maxPositionStart = -1;
                int maxPositionSize = -1; 

                // First we will encounter the height/widths, then later (in binary) the start/len
                for (int i = 0; i < bytes.Length - 1; i++)
                {
                    // Note we are not bounds checking here because if any of these fail, the preview file is invalid, and we just fall into the catch 
                    if (holdstart == -1)
                    {   // We are still in the prologue where adobe puts the list
                        if (System.Text.Encoding.UTF8.GetString(bytes, i, 6) == "height")
                        {
                            int.TryParse(System.Text.Encoding.UTF8.GetString(bytes, i + 9, LengthToComma(ref bytes, i + 9, 20)), out holdheight);  // just let parse sort out the comma
                        }
                        if (System.Text.Encoding.UTF8.GetString(bytes, i, 5) == "width")
                        {
                            int.TryParse(System.Text.Encoding.UTF8.GetString(bytes, i + 8, LengthToComma(ref bytes, i + 8, 20)), out holdwidth);  // just let parse sort out the comma
                        }
                        if (holdheight > 0 && holdwidth > 0)
                        {
                            startLen[++lastpositionsize, Heightpos] = holdheight;
                            startLen[lastpositionsize, Widthpos] = holdwidth;
                            holdheight = holdwidth = -1;
                        }
                    }

                    if (bytes[i] == 0xFF && bytes[i + 1] == 0xD8)
                    {   // start
                        holdstart = i;
                    }
                    else if (bytes[i] == 0xff && bytes[i + 1] == 0xD9)
                    {   // end
                        if (holdstart >= 0)
                        {
                            startLen[++lastpositionstart, Startpos] = holdstart;
                            startLen[lastpositionstart, Lenpos] = i - holdstart + 2;
                            int area; 
                            if((area = startLen[lastpositionstart,Heightpos] * startLen[lastpositionstart,Widthpos]) > maxSizeSoFar)
                            {
                                maxSizeSoFar = area;
                                maxPositionStart = startLen[lastpositionstart, Startpos];
                                maxPositionSize = startLen[lastpositionstart, Lenpos]; 
                            } 
                        }
                    }
                }

                using (MemoryStream ms = new MemoryStream(bytes, maxPositionStart, maxPositionSize))
                    using (Image img = Image.FromStream(ms))
                    {
                        img.Save(outFilePath);
                    Console.WriteLine("Wrote output file for {0} as {1}", cacheFilePath, outFilePath);
                    }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to process {0} due to error {1}", cacheFilePath, e.Message);
                return;
            }
        }
        private static int LengthToComma(ref byte[] bytes, int start, int maxlook)
        {
            // Used to look ahead in the image data to find the next comma; part of parsing out the file sizes from the header
            try
            {
                int ret = 1;
                for (int i = start; i - start < maxlook; i++)
                {
                    if (bytes[i] == ',')
                    {
                        return i - start;
                    }
                }
                return ret;
            }
            catch
            {
                return 1;
            }
        }
    }

}
