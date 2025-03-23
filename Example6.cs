using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class Example6
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
        public string GetItemsWithRejection(int length, int minmumSpecialCharacters)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            while (true)
            {
                char[] buffer = Random.Shared.GetItems<char>(characters, length);

                if ((buffer.Length - buffer.Count(char.IsAsciiLetterOrDigit)) >= minmumSpecialCharacters)
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

                if ((buffer.Length - buffer.Count(char.IsAsciiLetterOrDigit)) >= minmumSpecialCharacters)
                {
                    return new(buffer);
                }
            }
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string SpecialLoop(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[length];
            while (true)
            {
                Random.Shared.GetItems<char>(characters, buffer);
                int specialChars = 0;

                for (int i = 0; i < length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= minmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }
            }
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string SpecialLoopSecure(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[length];
            while (true)
            {
                RandomNumberGenerator.GetItems<char>(characters, buffer);
                int specialChars = 0;

                for (int i = 0; i < length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= minmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }
            }
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string StackAlloc(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            Span<char> buffer = stackalloc char[length];

            while (true)
            {
                Random.Shared.GetItems<char>(characters, buffer);

                int specialChars = 0;

                for (int i = 0; i < length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= minmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }

            }
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(24, 0)]
        [Arguments(24, 1)]
        [Arguments(24, 2)]
        public string StackAllocSecure(int length, int minmumSpecialCharacters)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            Span<char> buffer = stackalloc char[length];

            while (true)
            {
                RandomNumberGenerator.GetItems<char>(characters, buffer);

                int specialChars = 0;

                for (int i = 0; i < length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= minmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }

            }
        }

    }
}