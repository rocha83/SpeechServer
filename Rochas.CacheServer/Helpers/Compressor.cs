using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Rochas.CacheServer
{
    public static class Compressor
    {
        static GZipStream gzipStream = null;
        static MemoryStream memSource = null;
        static MemoryStream memDestination = null;

        public static byte[] ZipBinary(byte[] rawSource)
        {
            memDestination = new MemoryStream();
            memSource = new MemoryStream(rawSource);
            gzipStream = new GZipStream(memDestination, CompressionMode.Compress);

            memSource.CopyTo(gzipStream);

            gzipStream.Close();

            return memDestination.ToArray();
        }

        public static byte[] UnZipBinary(byte[] compressedSource)
        {
            byte[] unpackedContent = new byte[compressedSource.Length * 20];
            memSource = new MemoryStream(compressedSource);

            gzipStream = new GZipStream(memSource, CompressionMode.Decompress);

            var readedBytes = gzipStream.Read(unpackedContent, 0, unpackedContent.Length);

            memDestination = new MemoryStream(unpackedContent, 0, readedBytes);

            return memDestination.ToArray();
        }

        public static string ZipText(string rawText)
        {
            var cont = 0;
            byte[] rawBinary = null;
            byte[] compressedBinary = null;

            rawBinary = ASCIIEncoding.ASCII.GetBytes(rawText);

            compressedBinary = ZipBinary(rawBinary);

            return Convert.ToBase64String(compressedBinary);
        }

        public static string UnZipText(string compressedText)
        {
            string result = string.Empty;
            byte[] compressedBinary = Convert.FromBase64String(compressedText);
            byte[] destinBinary = UnZipBinary(compressedBinary);

            result = new string(ASCIIEncoding.ASCII.GetChars(destinBinary));

            return result.ToString();
        }
    }
}
