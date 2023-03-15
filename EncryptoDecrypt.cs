using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

var encryptoPk = CryptoHelper.EncryptPrivateKey("PRIVATE_KEY", "password");
Console.WriteLine(Encoding.UTF8.GetString(encryptoPk));

var decryptoPk = CryptoHelper.DecryptPrivateKey(encryptoPk, "password");
Console.WriteLine(decryptoPk);
public static class CryptoHelper
{
    private static byte[] Salt = GenerateSalt(32);
    private const int Iterations = 10000;
    private const int KeySize = 256;

    public static byte[] EncryptPrivateKey(string privateKey, string password)
    {
        //Salt = GenerateSalt(32);
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(password);
        Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(keyBytes, Salt, Iterations);
        byte[] keyArray = key.GetBytes(KeySize / 8);

        Aes aes = Aes.Create();
        aes.Key = keyArray;
        aes.GenerateIV();
        aes.Padding = PaddingMode.PKCS7;

        byte[] encryptedPrivateKey;

        using (MemoryStream ms = new MemoryStream())
        {
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] privateKeyBytes = System.Text.Encoding.UTF8.GetBytes(privateKey);
                cs.Write(privateKeyBytes, 0, privateKeyBytes.Length);
                cs.Close();
            }
            encryptedPrivateKey = ms.ToArray();
        }

        byte[] encryptedKeyArray = aes.IV.Concat(encryptedPrivateKey).ToArray();
        return encryptedKeyArray;
    }

    public static string DecryptPrivateKey(byte[] encryptedPrivateKeyArray, string password)
    {
        byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(password);
        Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(keyBytes, Salt, Iterations);
        byte[] keyArray = key.GetBytes(KeySize / 8);

        Aes aes = Aes.Create();
        aes.Key = keyArray;
        aes.IV = encryptedPrivateKeyArray.Take(aes.IV.Length).ToArray();
        aes.Padding = PaddingMode.PKCS7;

        string privateKey;

        using (MemoryStream ms = new MemoryStream())
        {
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                byte[] encryptedPrivateKeyBytes = encryptedPrivateKeyArray.Skip(aes.IV.Length).ToArray();
                cs.Write(encryptedPrivateKeyBytes, 0, encryptedPrivateKeyBytes.Length);
                cs.Close();
            }
            privateKey = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }

        return privateKey;
    }

    public static byte[] GenerateSalt(int saltSize)
    {
        using var randomNumberGenerator = new RNGCryptoServiceProvider();
        var salt = new byte[saltSize];
        randomNumberGenerator.GetBytes(salt);
        return salt;
    }

}

