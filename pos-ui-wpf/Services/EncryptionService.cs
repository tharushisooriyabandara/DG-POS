using System;
using System.IO;
using System.Security.Cryptography;

namespace POS_UI.Services
{
    public static class EncryptionService
    {
        // AES-GCM with 256-bit key
        public static byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (key == null || key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256-bit)", nameof(key));

            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[16];

            using (var aes = new AesGcm(key))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, null);
            }

            // Layout: [nonce(12)][tag(16)][ciphertext]
            using var ms = new MemoryStream();
            ms.Write(nonce, 0, nonce.Length);
            ms.Write(tag, 0, tag.Length);
            ms.Write(ciphertext, 0, ciphertext.Length);
            return ms.ToArray();
        }

        public static byte[] Decrypt(byte[] payload, byte[] key)
        {
            if (payload == null || payload.Length < 12 + 16) throw new ArgumentException("Payload too short", nameof(payload));
            if (key == null || key.Length != 32) throw new ArgumentException("Key must be 32 bytes (256-bit)", nameof(key));

            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            Buffer.BlockCopy(payload, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(payload, 12, tag, 0, tag.Length);
            int cipherLen = payload.Length - 28;
            byte[] ciphertext = new byte[cipherLen];
            Buffer.BlockCopy(payload, 28, ciphertext, 0, cipherLen);

            byte[] plaintext = new byte[cipherLen];
            using (var aes = new AesGcm(key))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext, null);
            }
            return plaintext;
        }
    }
}


