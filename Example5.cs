using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class Example5
    {

        [BenchmarkCategory("Vulnerable"), Benchmark(Baseline = true)]
        [Arguments(24, 0)]
        public string GeneratePassword(int length, int minmumSpecialCharacters)
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

        [BenchmarkCategory("Secure"), Benchmark(Baseline = true)]
        [Arguments(24, 0)]
        public string SecureRandom(int length, int minmumSpecialCharacters)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }


        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string RejectionSample(int length, int minmumSpecialCharacters)
        {

            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = new byte[length];
            char[] buffer = new char[length];


            while (true)
            {
                Random.Shared.NextBytes(bytebuffer);
                int specialChars = 0;
                bool metMinimum = false;

                for (int i = 0; i < length; i++)
                {
                    if (!metMinimum && (bytebuffer[i] > 54) && (++specialChars >= minmumSpecialCharacters))
                    {
                        metMinimum = true;
                    }

                    buffer[i] = characters[bytebuffer[i] % 64];
                }

                if (metMinimum)
                {

                    return new(buffer);
                }
            }

        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string RejectionSampleSecure(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = new byte[length];
            char[] buffer = new char[length];

            while (true)
            {
                RandomNumberGenerator.Fill(bytebuffer);
                int specialChars = 0;
                bool metMinimum = false;

                for (int i = 0; i < length; i++)
                {
                    if (!metMinimum && (bytebuffer[i] > 54) && (++specialChars >= minmumSpecialCharacters))
                    {
                        metMinimum = true;
                    }

                    buffer[i] = characters[bytebuffer[i] % 64];
                }

                if (metMinimum)
                {

                    return new(buffer);
                }
            }
        }




        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string GetItemsWithRejection(int length, int minmumSpecialCharacters)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            while (true)
            {
                char[] buffer = Random.Shared.GetItems<char>(characters, length);

                if (buffer.Count(char.IsAsciiLetterOrDigit) >= minmumSpecialCharacters)
                {
                    return new(buffer);
                }
            }


        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string GetItemsWithRejectionSecure(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            while (true)
            {
                char[] buffer = RandomNumberGenerator.GetItems<char>(characters, length);

                if (buffer.Count(char.IsAsciiLetterOrDigit) >= minmumSpecialCharacters)
                {
                    return new(buffer);
                }
            }
        }

    }
}