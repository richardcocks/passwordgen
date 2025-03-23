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
        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        [Params(0, 1, 2)]
        public int MinmumSpecialCharacters { get; set; }

        [Params(14, 24, 32)]
        public int Length { get; set; }

        [BenchmarkCategory("Vulnerable"), Benchmark(Baseline = true)]
        public string GeneratePassword()
        {
            string password = "";
            System.Random random = new System.Random();

            for (int i = 0; i < Length; i++)
            {
                password += characters[random.Next(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Secure"), Benchmark(Baseline = true)]
        public string SecureRandom()
        {
            string password = "";

            for (int i = 0; i < Length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string GetItemsWithRejection()
        {
            while (true)
            {
                char[] buffer = Random.Shared.GetItems<char>(characters, Length);

                if ((buffer.Length - buffer.Count(char.IsAsciiLetterOrDigit)) >= MinmumSpecialCharacters)
                {
                    return new(buffer);
                }
            }

        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string GetItemsWithRejectionSecure()
        {
            while (true)
            {
                char[] buffer = RandomNumberGenerator.GetItems<char>(characters, Length);

                if ((buffer.Length - buffer.Count(char.IsAsciiLetterOrDigit)) >= MinmumSpecialCharacters)
                {
                    return new(buffer);
                }
            }
        }


        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string SpecialLoop()
        {
            char[] buffer = new char[Length];
            while (true)
            {
                Random.Shared.GetItems<char>(characters, buffer);
                int specialChars = 0;

                for (int i = 0; i < Length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }
            }
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string SpecialLoopSecure()
        {
            char[] buffer = new char[Length];
            while (true)
            {
                RandomNumberGenerator.GetItems<char>(characters, buffer);
                int specialChars = 0;

                for (int i = 0; i < Length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }
            }
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string StackAlloc()
        {

            Span<char> buffer = stackalloc char[Length];

            while (true)
            {
                Random.Shared.GetItems<char>(characters, buffer);

                int specialChars = 0;

                for (int i = 0; i < Length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }

            }
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string StackAllocSecure()
        {

            Span<char> buffer = stackalloc char[Length];

            while (true)
            {
                RandomNumberGenerator.GetItems<char>(characters, buffer);

                int specialChars = 0;

                for (int i = 0; i < Length; i++)
                {
                    if (!char.IsAsciiLetterOrDigit(buffer[i]) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        return new(buffer);
                    }
                }

            }
        }
    }
}