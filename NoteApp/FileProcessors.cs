using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace NoteApp
{
    /// <summary>
    /// StreamFactory — создаёт потоки для чтения/записи с опциональной
    /// компрессией (GZip) и шифрованием (AES). Header (salt+iv) пишется
    /// в начале файла в незашифрованном виде.
    /// </summary>
    public static class StreamFactory
    {
        private const int SaltSize = 16; // байт
        private const int Iterations = 100_000;

        /// <summary>
        /// Открыть поток для записи. Возвращаемый Stream — тот, в который нужно писать данные (т.е. внешний).
        /// При закрытии возвращённого потока закроются все внутренние потоки.
        /// Порядок записи: записать header (salt+iv) в начало файла, затем (при encrypt) создать CryptoStream, затем (при compress) GZip поверх Crypto.
        /// Пользователь пишет в внешний поток plain-text.
        /// </summary>
        public static Stream OpenWriteDecorated(string path, bool compress, bool encrypt, string password)
        {
            if (encrypt && string.IsNullOrEmpty(password))
                throw new ArgumentException("Password required when encryption is requested.", nameof(password));

            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Stream current = fileStream;

            // Если шифрование — сгенерируем salt+iv, запишем их в файл (header), затем создадим CryptoStream на fileStream (позиция уже после header).
            if (encrypt)
            {
                // Salt
                byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

                // AES для генерации IV (BlockSize/8)
                using var tmpAes = Aes.Create();
                tmpAes.GenerateIV();
                byte[] iv = tmpAes.IV;

                // Записываем header (salt + iv) в начале файла (незашифрованно)
                fileStream.Write(salt, 0, salt.Length);
                fileStream.Write(iv, 0, iv.Length);
                fileStream.Flush();

                var key = DeriveKey(password, salt, tmpAes.KeySize / 8);

                var aes = Aes.Create();
                aes.KeySize = tmpAes.KeySize;
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var encryptor = aes.CreateEncryptor();

                var cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write, leaveOpen: false);

                current = cryptoStream;
                
            }

            if (compress)
            {
                .
                current = new GZipStream(current, CompressionLevel.Optimal, leaveOpen: false);
            }


            return current; 
        }

        
        public static Stream OpenReadDecorated(string path, bool compress, bool encrypt, string password)
        {
            if (encrypt && string.IsNullOrEmpty(password))
                throw new ArgumentException("Password required when decryption is requested.", nameof(password));

            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            Stream current = fileStream;

            // Если шифрование — сначала читаем header (salt+iv) из начала файла,
            // затем создаём CryptoStream (decrypt) поверх fileStream (position уже после header).
            if (encrypt)
            {
                // читаем salt и iv
                byte[] salt = new byte[SaltSize];
                int got = fileStream.Read(salt, 0, salt.Length);
                if (got != salt.Length)
                {
                    fileStream.Dispose();
                    throw new InvalidDataException("File header (salt) is missing or corrupted.");
                }

                // iv длина определяется по BlockSize AES (обычно 16)
                using var tmpAes = Aes.Create();
                int ivLen = tmpAes.BlockSize / 8;
                byte[] iv = new byte[ivLen];
                got = fileStream.Read(iv, 0, ivLen);
                if (got != ivLen)
                {
                    fileStream.Dispose();
                    throw new InvalidDataException("File header (IV) is missing or corrupted.");
                }

                var key = DeriveKey(password, salt, tmpAes.KeySize / 8);

                var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var decryptor = aes.CreateDecryptor();

                // CryptoStream поверх fileStream (fileStream.Position уже после header)
                // leaveOpen:false — закрытие crypto закроет fileStream.
                var cryptoStream = new CryptoStream(fileStream, decryptor, CryptoStreamMode.Read, leaveOpen: false);

                current = cryptoStream;
                // Если compress==true, поверх cryptoStream мы создадим GZipStream(Decompress)
            }

            if (compress)
            {
                // GZipStream поверх current (cryptoStream или fileStream). leaveOpen:false: закрытие внешнего закроет всё.
                current = new GZipStream(current, CompressionMode.Decompress, leaveOpen: false);
            }

            return current; // внешний поток для чтения plain-text
        }

        private static byte[] DeriveKey(string password, byte[] salt, int keyBytes)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(keyBytes);
        }
    }
}
