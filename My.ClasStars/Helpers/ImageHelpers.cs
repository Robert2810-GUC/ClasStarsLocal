using System;
using System.IO;

namespace My.ClasStars.Helpers
{
    public static class ImageHelpers
    {
        public enum EditorImageFormat { Unknown, Jpeg, Png }

        public static EditorImageFormat GetImageFormatFromStream(MemoryStream ms)
        {
            byte[] header = new byte[8];
            ms.Position = 0;
            _ = ms.Read(header, 0, header.Length);
            ms.Position = 0;

            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return EditorImageFormat.Jpeg;
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A) return EditorImageFormat.Png;
            return EditorImageFormat.Unknown;
        }

        public static bool? IsSquareImage(MemoryStream stream, out string err)
        {
            err = "";
            try
            {
                using var image = System.Drawing.Image.FromStream(stream);
                return Math.Abs(image.Width - image.Height) <= 2;
            }
            catch (Exception e)
            {
                err = e.Message;
                return null;
            }
        }
    }
}
