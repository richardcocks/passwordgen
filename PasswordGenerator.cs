using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    [MemoryDiagnoser]
    [RPlotExporter]
    public class PasswordGenerator
    {

        [BenchmarkCategory("Vulnerable"), Benchmark(Baseline = true)]
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

        [BenchmarkCategory("Secure"), Benchmark(Baseline = true)]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGNaive(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGStringBuilder(int length)
        {
            StringBuilder password = new(length);
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < length; i++)
            {
                password.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
            }
            return password.ToString();
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGFillArray(int length)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGReducedCharacterSet(int length)
        {
            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGReduceSetBuffer(int length)
        {
            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = RandomNumberGenerator.GetBytes(length);

            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGAvoidMod(int length)
        {
            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = RandomNumberGenerator.GetBytes(length);

            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[bytebuffer[i]];
            }

            return new(buffer);
        }


        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RandomChoices(int length)
        {

            char[] buffer = Random.Shared.GetItems<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+", length);
            return new(buffer);
        }


        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string RNGChoices(int length)
        {

            char[] buffer = RandomNumberGenerator.GetItems<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+", length);
            return new(buffer);
        }




    }
}