using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example1
    {

        [Benchmark(Baseline = true)]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GeneratePassword(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            System.Random random = new System.Random();

            for (int i = 0; i < length; i++)
            {
                password += characters[random.Next(characters.Length)];
            }
            return password;
        }

        [Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string SecureRandom(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GeneratePasswordSharedRandom(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";


            for (int i = 0; i < length; i++)
            {
                password += characters[Random.Shared.Next(characters.Length)];
            }
            return password;
        }

    }
}