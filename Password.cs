using System.Text;
using BenchmarkDotNet.Attributes;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [RPlotExporter]
    public class PasswordGenerator
    {

        char[] ByteToCharLookup = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ];

        [Params(10, 20)]
        public int n;

        [Benchmark(Baseline = true)]
        public string PasswordChatGpt()
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            System.Random random = new System.Random();

            for (int i = 0; i < n; i++)
            {
                password += characters[random.Next(characters.Length)];
            }
            return password;
        }

        [Benchmark]
        public string PasswordChatGptSharedRandom()
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < n; i++)
            {
                password += characters[Random.Shared.Next(characters.Length)];
            }
            return password;
        }

        [Benchmark]
        public string PasswordChatGptStringBuilder()
        {
            StringBuilder password = new(n);
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < n; i++)
            {
                password.Append(characters[Random.Shared.Next(characters.Length)]);
            }
            return password.ToString();
        }

        [Benchmark]
        public string PasswordUsingCharBuffer()
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }
            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookup()
        {
            char[] characters = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                                  'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                                  'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '!', '@', '#',
                                  '$', '%', '^', '&', '*', '(', ')', '_', '+' ];

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }
            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookupSpan()
        {
            ReadOnlySpan<char> characters = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                                  'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                                  'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '!', '@', '#',
                                  '$', '%', '^', '&', '*', '(', ')', '_', '+' ];

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }
            return new(buffer);
        }


        [Benchmark]
        public string PasswordUsingCharBufferCharLookupReducedSet()
        {
            ReadOnlySpan<char> characters = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ];

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }
            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookupReducedSetPregen()
        {
            ReadOnlySpan<char> characters = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ];

            byte[] bytebuffer = new byte[n];

            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookupReducedSetPregenNoMod()
        {
            ReadOnlySpan<char> characters = [ 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ,
                                  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm',
                                  'n', 'p', 'q', 'r', 's', 't', 'u', 'w', 'x', 'y', 'z',
                                  'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M',
                                  'N', 'P', 'Q', 'R', 'S', 'T', 'V', 'W', 'X', 'Y', 'Z',
                                  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@', '#',
                                  '$', '%', '&', '(', ')', '_', '+' ];

            byte[] bytebuffer = new byte[n];

            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = characters[bytebuffer[i]];
            }

            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookupReducedSetPregenNoModOutsideCall()
        {

            byte[] bytebuffer = new byte[n];

            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[n];

            for (int i = 0; i < n; i++)
            {
                buffer[i] = ByteToCharLookup[bytebuffer[i]];
            }

            return new(buffer);
        }

        [Benchmark]
        public string PasswordUsingCharBufferCharLookupReducedSetPregenNoModOutsideCallArrayConvert()
        {

            byte[] bytebuffer = new byte[n];

            Random.Shared.NextBytes(bytebuffer);

            return new(Array.ConvertAll(bytebuffer, x => ByteToCharLookup[x]));
        }


    }
}