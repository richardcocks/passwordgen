using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example7
    {
        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        private const string charactersLongSet = "!@#$%^&*()_+!@#$%^&*()_+!@#$%^&*()_+abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Params(32, 48, 64, 128, 256, 512, 1024)]
        public int RandomBufferLength { get; set; }

        [Params(8)]
        public int MinmumSpecialCharacters { get; set; }

        [Params(32)]
        public int Length { get; set; }

        [Benchmark]
        public string RandomBufferLengthsTest()
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