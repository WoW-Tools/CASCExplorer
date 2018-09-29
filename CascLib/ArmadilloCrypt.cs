using System;
using System.IO;
using System.Security.Cryptography;

namespace CASCLib
{
    public class ArmadilloCrypt
    {
        private byte[] _key;

        public byte[] Key => _key;

        public ArmadilloCrypt(byte[] key)
        {
            _key = key;
        }

        public ArmadilloCrypt(string keyName)
        {
            if (!LoadKeyFile(keyName, out _key))
                throw new ArgumentException("keyName");
        }

        static bool LoadKeyFile(string keyName, out byte[] key)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            FileInfo fi = new FileInfo(Path.Combine(appDataPath, "Battle.net\\Armadillo", keyName + ".ak"));

            key = null;

            if (!fi.Exists)
                return false;

            if (fi.Length != 20)
                return false;

            using (var file = fi.OpenRead())
            {
                byte[] keyBytes = new byte[16];

                if (file.Read(keyBytes, 0, keyBytes.Length) != 16)
                    return false;

                byte[] checkSum = new byte[4];

                if (file.Read(checkSum, 0, checkSum.Length) != 4)
                    return false;

                byte[] keyMD5;

                using (MD5 md5 = MD5.Create())
                {
                    keyMD5 = md5.ComputeHash(keyBytes);
                }

                // check first 4 bytes
                for (int i = 0; i < checkSum.Length; i++)
                {
                    if (checkSum[i] != keyMD5[i])
                        return false;
                }

                key = keyBytes;
            }

            return true;
        }

        public byte[] DecryptFile(string name)
        {
            using (FileStream fs = new FileStream(name, FileMode.Open))
                return DecryptFile(name, fs);
        }

        public byte[] DecryptFile(string name, Stream stream)
        {
            string fileName = Path.GetFileNameWithoutExtension(name);

            if (fileName.Length != 32)
                throw new ArgumentException("name");

            byte[] IV = fileName.Substring(16).ToByteArray();

            ICryptoTransform decryptor = KeyService.SalsaInstance.CreateDecryptor(_key, IV);

            using (CryptoStream cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
            using (MemoryStream ms = new MemoryStream())
            {
                cs.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public Stream DecryptFileToStream(string name, Stream stream)
        {
            string fileName = Path.GetFileNameWithoutExtension(name);

            if (fileName.Length != 32)
                throw new ArgumentException("name");

            byte[] IV = fileName.Substring(16).ToByteArray();

            ICryptoTransform decryptor = KeyService.SalsaInstance.CreateDecryptor(_key, IV);

            using (CryptoStream cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read))
            {
                MemoryStream ms = new MemoryStream();
                cs.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
        }
    }
}
