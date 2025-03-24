using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example5
    {

        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        [Params(0, 1, 2)]
        public int MinmumSpecialCharacters { get; set; }

        [Params(14, 24, 32)]
        public int Length { get; set; }

        [Benchmark(Baseline = true)]
        public string GetItemsSecure()
        {
            char[] buffer = RandomNumberGenerator.GetItems<char>(characters, Length);
            return new(buffer);
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


    }
}