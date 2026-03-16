using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using static Alchera.FaceSDK.FaceSDK;

namespace Alchera.FaceSDK
{
    public class ZipUtil
    {
        public enum CompressLvl
        {
            NO_COMPRESS,
            OPTIMAL,
            FASTEST,
        }

        public static int ResolveZipCompressLvl(CompressLvl level)
        {
            switch (level) {
                case CompressLvl.NO_COMPRESS: return 0;
                case CompressLvl.FASTEST: return 3;
                case CompressLvl.OPTIMAL: return 9;
                default: return 9;
            }
        }
    
        public static byte[] CreateZipStream(byte[][] srcs, string file_fmt="{0}",
            int zip_entry_filename_start_idx = 1, string enc_passwd = "", int enc_key_siz_bit = 256, 
            CompressLvl level = CompressLvl.OPTIMAL)
        {
            if (srcs is null)
                return null;

            var ms = new MemoryStream();

            if (file_fmt == "")
                file_fmt = "{0}";

            if (enc_passwd == "")
                enc_key_siz_bit = 0;

            try
            {
                //CSharpCode.SharpZipLib
                using (var zip = new ZipOutputStream(ms))
                {
                    zip.IsStreamOwner = false;
                    zip.SetLevel(ResolveZipCompressLvl(level));

                    if (enc_passwd != "")
                        zip.Password = enc_passwd;

                    for (int i = 0; i < srcs.Length; i++)
                    {
                        string filename = string.Format(file_fmt, zip_entry_filename_start_idx + i);

                        var entry = new ZipEntry(filename) {
                            DateTime = DateTime.UtcNow
                        };

                        if (enc_key_siz_bit == 128 || enc_key_siz_bit == 256)
                            entry.AESKeySize = enc_key_siz_bit;

                        zip.PutNextEntry(entry);

                        var data = srcs[i] ?? Array.Empty<byte>();
                        zip.Write(data, 0, data.Length);

                        zip.CloseEntry();
                    }

                    zip.Finish();
                }

            }
            catch (Exception e)
            {
                //Console.WriteLine(e.Message);
                return null;
            }

            return ms.ToArray();
        }




    }


    public class ImgUtil
    {
        //
        // jpeg util
        //

        public static class JpgEncodeWin
        {
            public static byte[] EncodeBGR(byte[] bgr_img, int width, int height, int quality = 100, int? srcStrideForRowPadding = null)
            {
                if (bgr_img == null) throw new ArgumentNullException(nameof(bgr_img));
                if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();

                // PixelFormat.Format24bppRgb => Stored in memroy by [ Blue,Green,Red ]
                var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

                try
                {
                    int dstStride = Math.Abs(data.Stride);
                    int rowSize = width * 3; // bgr

                    if (srcStrideForRowPadding.HasValue && srcStrideForRowPadding.Value != rowSize)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var srcOff = y * srcStrideForRowPadding.Value;
                            var dstOff = y * dstStride;
                            Marshal.Copy(bgr_img, srcOff, data.Scan0 + dstOff, rowSize);
                        }
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var srcOff = y * rowSize;
                            var dstOff = y * dstStride;
                            Marshal.Copy(bgr_img, srcOff, data.Scan0 + dstOff, rowSize);
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                var ms = new MemoryStream();

                ImageCodecInfo jpg_enc_info = null;

                ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
                foreach (var c in codecs)
                {
                    if (c.FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpg_enc_info = c;
                        break;
                    }
                }

                if(jpg_enc_info == null)
                    return null;

                var enc_prm = new EncoderParameters(1);

                enc_prm.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                bmp.Save(ms, jpg_enc_info, enc_prm);

                return ms.ToArray();
            }
           
        }


        //
        // convert bgr-raw to jpg
        //
               
        public static byte[] EncBGRToJpg(byte[] bgr_pixels, int width, int height, int jpg_quality=100)
        {
            return JpgEncodeWin.EncodeBGR(bgr_pixels, width, height, jpg_quality);
        }

        public static BlobImg ConvBGRImgToJpg(BlobImg img, int jpg_quality = 100)
        {
            if (img is null)
                return null;

            byte[] jpg_enc = EncBGRToJpg(img.data_, img.width_, img.height_, jpg_quality);

            if (jpg_enc is null)
                return null;

            return new BlobImg(jpg_enc, img.width_, img.height_, "jpg," + jpg_quality);
        }

        public static BlobImg[] ConvBGRImgsToJpg(BlobImg[] imgs, int jpg_quality = 100)
        {
            if (imgs is null)
                return null;

            BlobImg[] jpg_encs = new BlobImg[imgs.Length];

            for(int i=0; i<imgs.Length; i++)
            {
                BlobImg jpg_enc = ConvBGRImgToJpg(imgs[i], jpg_quality);

                if (jpg_enc == null)
                    return null;

                jpg_encs[i] = jpg_enc;
            }

            return jpg_encs;
        }

        public static byte[] CreateZipStreamFromBGR(BlobImg[] imgs, string fmt, 
            int zip_entry_filename_start_idx = 1, string passwd="")
        {
            if (imgs is null)
                return null;

            byte[][] src = new byte[imgs.Length][];

            for (int i = 0; i < imgs.Length; i++)
                src[i] = imgs[i].data_;

            int enc_key_siz_bit = 0;

            if (passwd != "")
                enc_key_siz_bit = 256; // using aes-256-cbc

            return ZipUtil.CreateZipStream(src, fmt, 
                zip_entry_filename_start_idx, passwd, enc_key_siz_bit);
        }


        //
        // convert to bgr
        //

        public static byte[] ConvToBGR(Bitmap bitmap)
        {
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                throw new ArgumentException("Input bitmap must be in Format32bppARGB pixel format.", nameof(bitmap));
            }

            // pixels_bgr[0]: Blue , pixels_bgr[1]: Green , pixels_bgr[2]: Red
            byte[] pixels_bgr = new byte[bitmap.Width * bitmap.Height * 3];
            int pixels_bgr_pos = 0;

            BitmapData bmpData = null;

            try
            {
                Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

                IntPtr ptr = bmpData.Scan0;
                int stride = bmpData.Stride;
                int width = bitmap.Width;
                int height = bitmap.Height;

                byte[] rowPixelsARGB = new byte[stride];

                for (int y = 0; y < height; y++)
                {
                    IntPtr rowPtr = new IntPtr(ptr.ToInt64() + (y * stride));

                    Marshal.Copy(rowPtr, rowPixelsARGB, 0, stride);

                    for (int x = 0; x < width; x++)
                    {
                        int pixelOffsetInRow = x * 4; // 4 byte: ARGB

                        // BGRA -> BGR
                        pixels_bgr[pixels_bgr_pos] = rowPixelsARGB[pixelOffsetInRow];     // Blue
                        pixels_bgr[pixels_bgr_pos + 1] = rowPixelsARGB[pixelOffsetInRow + 1]; // Green
                        pixels_bgr[pixels_bgr_pos + 2] = rowPixelsARGB[pixelOffsetInRow + 2]; // Red
                        pixels_bgr_pos += 3;
                    }
                }
            }
            finally
            {
                if (bmpData != null)
                {
                    bitmap.UnlockBits(bmpData);
                }
            }

            return pixels_bgr;
        }
        
        // crop left/right side with aspect 720:1280 (16:9)
        // > calculate new width = img_height * (720/1280)
        public class CropImgLeftRightRst
        {
            public byte[] pixels_bgr;
            public int width;
            public int height;
        };

        public static CropImgLeftRightRst CropImgLeftRightByRatio(byte[] pixels_bgr, int width, int height,
            float new_width_aspect = 720.0F / 1280.0F)
        {
            double targetAspect = (double)new_width_aspect;
            double currentAspect = (double)width / height;

            int newWidth;
            int newHeight = height;

            newWidth = (int)Math.Round(newHeight * targetAspect);

            int cropWidth = width - newWidth;

            if (cropWidth < 0)
            {
                newWidth = width;
                cropWidth = 0;
            }

            int cropLeft = cropWidth / 2;

            int bytesPerPixel = 3; // BGR
            int originalStride = width * bytesPerPixel;
            int newStride = newWidth * bytesPerPixel;

            byte[] newBgrData = new byte[newWidth * newHeight * bytesPerPixel];

            for (int y = 0; y < height; y++)
            {
                int originalRowStart = y * originalStride + (cropLeft * bytesPerPixel);
                int newRowStart = y * newStride;
                Buffer.BlockCopy(pixels_bgr, originalRowStart, newBgrData, newRowStart, newStride);
            }

            return new CropImgLeftRightRst {
                pixels_bgr   = newBgrData,
                width        = newWidth,
                height       = newHeight
            };
        }

        public static BlobImg CropImgByAspectRatio(byte[] pixels_bgr, int width, int height, 
            float target_aspect = 720.0F / 1280.0F)
        {
            float cur_aspect = width / height;

            int bytesPerPixel = 3; // BGR
            int originalStride = width * bytesPerPixel;                        

            if (width > height)
            {
                // clip left-right
                int newWidth = (int)Math.Round(height * target_aspect);
                int newHeight = height;

                int cropWidth = width - newWidth;

                if (cropWidth < 0)
                {
                    newWidth = width;
                    cropWidth = 0;
                }

                int cropLeft = cropWidth / 2;
                int newStride = newWidth * bytesPerPixel;

                byte[] newBgrData = new byte[newWidth * newHeight * bytesPerPixel];

                for (int y = 0; y < height; y++)
                {
                    int originalRowStart = y * originalStride + (cropLeft * bytesPerPixel);
                    int newRowStart = y * newStride;
                    Buffer.BlockCopy(pixels_bgr, originalRowStart, newBgrData, newRowStart, newStride);
                }

                return new BlobImg(newBgrData, newWidth, newHeight);
            }
            else
            {
                // clip top-bottom
                int newWidth = width;
                int newHeight = (int)Math.Round(height * target_aspect);

                int cropHeight = height - newHeight;

                if (cropHeight < 0)
                {
                    newHeight = height;
                    cropHeight = 0;
                }

                int cropTop = cropHeight / 2;
                int newStride = newWidth * bytesPerPixel;
                byte[] newBgrData = new byte[newWidth * newHeight * bytesPerPixel];

                for (int y = cropTop; y < (cropTop + newHeight); y++)
                {
                    int originalRowStart = y * originalStride;
                    int newRowStart = (y - cropTop) * newStride;
                    Buffer.BlockCopy(pixels_bgr, originalRowStart, newBgrData, newRowStart, newStride);
                }

                CropImgLeftRightRst rst_left_right_crop
                    = CropImgLeftRightByRatio(newBgrData, newWidth, newHeight, target_aspect);

                return new BlobImg(rst_left_right_crop.pixels_bgr, rst_left_right_crop.width, rst_left_right_crop.height);
                //return new BlobImg(newBgrData, newWidth, newHeight);
            }
            
        }

        public static void SaveBGRAsBmp(string file_path, byte[] pixels_bgr, int width, int height)
        {
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.WriteOnly,
                    bmp.PixelFormat
                );

                int sourceStride = width * 3;
                int destinationStride = bmpData.Stride;

                int expectedSourceSize = sourceStride * height;

                if (pixels_bgr.Length < expectedSourceSize)
                {
                    bmp.UnlockBits(bmpData);
                    return;
                }

                for (int y = 0; y < height; y++)
                {
                    int sourceOffset = y * sourceStride;
                    IntPtr destScan0 = (IntPtr)((long)bmpData.Scan0 + (long)y * destinationStride);
                    int bytesToCopyThisRow = Math.Min(sourceStride, destinationStride);

                    if (sourceOffset + bytesToCopyThisRow > pixels_bgr.Length)
                    {
                        bytesToCopyThisRow = pixels_bgr.Length - sourceOffset;

                        if (bytesToCopyThisRow <= 0)
                            break;
                    }

                    Marshal.Copy(pixels_bgr, sourceOffset, destScan0, bytesToCopyThisRow);
                }

                bmp.UnlockBits(bmpData);

                try
                {
                    bmp.Save(file_path, ImageFormat.Bmp);
                    //Console.WriteLine($"Image successfully saved to: {file_path}");
                }
                catch (Exception e)
                {
                    //Console.WriteLine($"Error saving image to {file_path}: {e.Message}");
                }
            }
        }


        //
        // ImgLoader
        //

        public class ImgLoader
        {
            private ImgLoader() { }
            public ImgLoader(string img_path)
            {
                try
                {
                    img_path_ = img_path;

                    using (Bitmap bitmap = new Bitmap(img_path_))
                    {
                        int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                        int byteCount = bitmap.Width * bitmap.Height * bytesPerPixel;

                        width_ = bitmap.Width;
                        height_ = bitmap.Height;

                        pixels_bgr_ = new byte[bitmap.Width * bitmap.Height * 3];

                        if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb
                            && bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                        {
                            err_msg_ = "unsupported bitmap format";
                            return;
                        }

                        if (bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb)
                        {
                            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

                            System.Drawing.Imaging.BitmapData bmpData
                                = bitmap.LockBits(rect,
                                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                    bitmap.PixelFormat);

                            System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, pixels_bgr_, 0, byteCount);

                            bitmap.UnlockBits(bmpData);
                        }
                        else if (bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                        {
                            pixels_bgr_ = ConvToBGR(bitmap);
                        }
                    }
                }
                catch (Exception e)
                {
                    err_msg_ = e.Message.ToString();
                }
            }

            public bool IsErr()
            {
                return err_msg_ == null ? false : true;
            }

            // pixels_bgr_[0]=Blue
            // pixels_bgr_[1]=Green
            // pixels_bgr_[2]=Red
            public byte[] pixels_bgr_;

            public int width_;
            public int height_;

            public string err_msg_;
            public string img_path_;
        }

    } // namespace ImgUtil

}
