using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example8
    {
        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        private const string charactersLongSet = "!@#$%^&*()_+!@#$%^&*()_+!@#$%^&*()_+abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Params(64, 128, 256)]
        public int RandomBufferLength { get; set; }

        [Params(0, 1, 2)]
        public int MinmumSpecialCharacters { get; set; }

        [Params(14, 24, 32)]
        public int Length { get; set; }

        [Benchmark(Baseline = true)]
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

        [Benchmark]
        public string DoubleRejection()
        {
            Span<byte> bytebuffer = stackalloc byte[RandomBufferLength];
            Span<char> buffer = stackalloc char[Length];


            while (true)
            {
                int i = 0;
                RandomNumberGenerator.Fill(bytebuffer);
                int charIndex = 0;
                bool metMinimum = false;

                int specialChars = 0;

                while (i < RandomBufferLength)
                {
                    byte value = bytebuffer[i];
                    if (value >= 222)
                    {
                        i++;
                        continue;
                    }

                    buffer[charIndex] = charactersLongSet[bytebuffer[i]];

                    if (!metMinimum && (value < 36) && (++specialChars >= MinmumSpecialCharacters))
                    {
                        metMinimum = true;
                    }

                    if (++charIndex == Length)
                    {
                        if (metMinimum)
                        {
                            return new(buffer);
                        }
                        else
                        {
                            // reset charIndex
                            charIndex = 0;
                            specialChars = 0;
                            i++;
                            continue;

                        }

                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
            }
        }

    }
}