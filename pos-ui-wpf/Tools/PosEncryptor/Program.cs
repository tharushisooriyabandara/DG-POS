using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

static class AesGcmEnc
{
    public static byte[] Encrypt(byte[] plaintext, byte[] key)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        using var ms = new MemoryStream();
        ms.Write(nonce, 0, nonce.Length);
        ms.Write(tag, 0, tag.Length);
        ms.Write(ciphertext, 0, ciphertext.Length);
        return ms.ToArray();
    }
}

class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: PosEncryptor <inputPath> <outputPath> <usbKeyFileName>");
            return 0; // non-fatal for msbuild
        }

        string inputPath = args[0];
        string outputPath = args[1];
        string usbKeyFileName = args[2];

        try
        {
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Skip: input missing {inputPath}");
                return 0;
            }

            string FindKeyPath()
            {
                foreach (var d in DriveInfo.GetDrives().OrderByDescending(d => d.DriveType == DriveType.Removable))
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        var p = Path.Combine(d.RootDirectory.FullName, usbKeyFileName);
                        if (File.Exists(p)) return p;
                    }
                    catch { }
                }
                return string.Empty;
            }

            var keyPath = FindKeyPath();
            if (string.IsNullOrEmpty(keyPath))
            {
                Console.Error.WriteLine($"Key not found on any drive: {usbKeyFileName}");
                return 1;
            }

            var keyB64 = File.ReadAllText(keyPath).Trim();
            byte[] key;
            try { key = Convert.FromBase64String(keyB64); }
            catch { Console.Error.WriteLine("Invalid Base64 key"); return 1; }
            if (key.Length != 32) { Console.Error.WriteLine("Key must be 32 bytes"); return 1; }

            var plain = File.ReadAllBytes(inputPath);
            var enc = AesGcmEnc.Encrypt(plain, key);
            File.WriteAllBytes(outputPath, enc);
            Console.WriteLine($"Encrypted {inputPath} -> {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}


