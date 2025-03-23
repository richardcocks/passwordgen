using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    [MemoryDiagnoser]
    public class PasswordGenerator
    {
        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        [Params(14, 24, 32)]
        public int Length { get; set; }

        [Params(0, 1, 2)]
        public int MinmumSpecialCharacters { get; set; }

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
        public string GeneratePasswordSharedRandom()
        {
            string password = "";


            for (int i = 0; i < Length; i++)
            {
                password += characters[Random.Shared.Next(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string StringBuilder()
        {

            StringBuilder password = new(Length);
            for (int i = 0; i < Length; i++)
            {
                password.Append(characters[Random.Shared.Next(characters.Length)]);
            }

            return password.ToString();
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string StringBuilderSecure()
        {


            StringBuilder password = new(Length);
            for (int i = 0; i < Length; i++)
            {
                password.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
            }

            return password.ToString();
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string CharArray()
        {

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }

            return new string(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string CharArraySecure()
        {
            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }

            return new string(buffer);
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string Buffer()
        {

            byte[] bytebuffer = new byte[Length];
            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = charactersShortSet[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string BufferSecure()
        {
            byte[] bytebuffer = new byte[Length];
            RandomNumberGenerator.Fill(bytebuffer);

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = charactersShortSet[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string GetItems()
        {


            char[] buffer = Random.Shared.GetItems<char>(characters, Length);

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string GetItemsSecure()
        {

            char[] buffer = RandomNumberGenerator.GetItems<char>(characters, Length);

            return new(buffer);
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string RejectionSample()
        {


            byte[] bytebuffer = new byte[Length];
            char[] buffer = new char[Length];


            while (true)
            {
                Random.Shared.NextBytes(bytebuffer);
                int specialChars = 0;
                bool metMinimum = false;

                for (int i = 0; i < Length; i++)
                {
                    if (!metMinimum && (bytebuffer[i] > 54) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        metMinimum = true;
                    }

                    buffer[i] = charactersShortSet[bytebuffer[i] % 64];
                }

                if (metMinimum)
                {

                    return new(buffer);
                }
            }

        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string RejectionSampleSecure()
        {

            byte[] bytebuffer = new byte[Length];
            char[] buffer = new char[Length];

            while (true)
            {
                RandomNumberGenerator.Fill(bytebuffer);
                int specialChars = 0;
                bool metMinimum = false;

                for (int i = 0; i < Length; i++)
                {
                    if (!metMinimum && (bytebuffer[i] > 54) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        metMinimum = true;
                    }

                    buffer[i] = charactersShortSet[bytebuffer[i] % 64];
                }

                if (metMinimum)
                {

                    return new(buffer);
                }
            }
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