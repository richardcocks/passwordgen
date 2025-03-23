# Fixing a password generator

I've been [nerd-sniped](https://xkcd.com/356/) by co-pilot. I don't normally have it enabled, but was working on another machine which did. I was implementing a feature when it suggested autocompleting GeneratePassword.

It took me on a journey of benchmarking with BenchmarkDotNet and discovering what did and did not affect performance in microbenchmarking.

## Co-pilot output

The original suggestion, straight from co-pilot was:

```csharp
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

```

It surprised me, because it's in the perfect spot of being just about good enough to sneak through some reviews. It would certainly get a few remarks if reviewed as a standalone feature, but it could get through if bundled as part of 20 files in a wider feature. This is a benefit of small commits, but that's a post for another day.

Yet it's also bad code. There are to me, three main concerns:

* Performance - The use of `new System.Random()` rather than `System.Random.Shared`, and string concatenation rather than `StringBuilder`, are two things in particular that stand out as quick wins for performance.
* Security - Using `System.Random` instead of a cryptographically secure generator such as `System.Security.Cryptography.RandomNumberGenerator` is a concern for any password generator.
* Lack of checking output for symbols - It's desirable for passwords to always have at least one symbol to pass most validation sets. With this password generator, you'd be fine most of the time given the input set, but then occassionally be frustrated when it generates a password without symbols. This would happen around 5% of the time for a 16 character password. Just often enough to get missed in testing then hit you later.

## Improving Security
Let's address security first, it's no good having a password generator be fast if you can't trust the output.

```csharp
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
```

I've kept the same naive implementation, but swapped `random.Next()` for the static (and thread safe) `RandomNumberGenerator.GetInt32` which is a more secure random number generator.


## Benchmarking and performance optimisation
Let's get this under the benchmark microscope to see how we can address performance.

Measuring baseline performance is easy with [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet), we just create a class and then annotate it. 


Since we're creating a class, let's move `length` and `characters` to properties in the class.

 This will also let us easily parameterise the length across our benchmarks. I've gone with 3 different lengths of passwords, so that we can see the effect of increasing password length on generation, and also used two separate categories so we can compare our optimisation efforts on both the secure and vulnerable versions of the password generator.


```csharp
[Benchmark()]
public string GeneratePasswordSharedRandom()
{
    string password = "";

    for (int i = 0; i < Length; i++)
    {
        password += characters[Random.Shared.Next(characters.Length)];
    }
    return password;
}
```

Let's immediately look at the impact of removing `System.Random random = new System.Random();` and using the static `System.Random.Shared` for the vulnerable example.

For this example I'll post the whole class, for future examples I'll just post individual methods.

<details>
<summary>Example1.cs</summary>

```csharp
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example1
    {
        [Params(14, 24, 32)]
        public int Length { get; set; }

        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

        [Benchmark(Baseline = true)]
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

        [Benchmark()]
        public string SecureRandom(int length)
        {
            string password = "";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [Benchmark()]
        public string GeneratePasswordSharedRandom()
        {
            string password = "";

            for (int i = 0; i < Length; i++)
            {
                password += characters[Random.Shared.Next(characters.Length)];
            }
            return password;
        }

    }
}
```

</details>

| Method                       | Length | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **GeneratePassword**             | **14**     |   **219.0 ns** |  **4.19 ns** |  **3.92 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 | 14     | 1,378.7 ns |  6.59 ns |  5.50 ns |  6.30 |    0.11 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom | 14     |   129.3 ns |  1.85 ns |  1.54 ns |  0.59 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**             | **24**     |   **307.6 ns** |  **4.08 ns** |  **3.41 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 | 24     | 2,379.0 ns | 11.01 ns |  9.20 ns |  7.73 |    0.09 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom | 24     |   227.1 ns |  1.20 ns |  0.93 ns |  0.74 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**             | **32**     |   **390.2 ns** |  **7.85 ns** |  **9.64 ns** |  **1.00** |    **0.03** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 | 32     | 3,181.0 ns | 18.73 ns | 15.64 ns |  8.16 |    0.20 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom | 32     |   314.4 ns |  6.03 ns |  6.20 ns |  0.81 |    0.02 | 0.2217 |    1856 B |        0.96 |



We can now see that avoiding `new System.Random()` increased performance, roughly 35% faster for the 24 character example.

We can also see that using `RandomNumberGenerator.GetInt32` destroyed our performance, taking us into the microsecond territory and taking around 8 times as long to do the same work.

To make future comparisons easier, we can add `[BenchmarkCategory("Secure")]` and `[BenchmarkCategory("Vulnerable")]` attributes to our benchmarks to mark `GeneratePassword` and `SecureRandom` as two separate baselines, so we can more easily examine the performane impact on changes made to each. We also need to mark the class with `[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]` and `[CategoriesColumn]` to get a category column in the output table.

## String Building
Okay, let's do the other straightforward and obvious improvement, to use `StringBuilder`, so that our non-secure version now looks like this:

```csharp
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

// With an equivalent Secure version not shown here.

```
We know the size of the string, so we were able to intialize our string builder with that capacity. But given we know the size of the string, wouldn't it be faster still to allocate a `char[]` and fill it? Let's try that at the same time and compare:

```csharp
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

// With an equivalent Vulnerable version not shown here.
```

Here are the results:

| Method              | Categories | Length | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------- |------- |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**        | **Secure**     | **14**     | **1,406.36 ns** | **24.936 ns** | **22.105 ns** | **1,399.08 ns** |  **1.00** |    **0.02** | **0.0668** |     **560 B** |        **1.00** |
| StringBuilderSecure | Secure     | 14     | 1,183.25 ns | 22.336 ns | 21.937 ns | 1,175.49 ns |  0.84 |    0.02 | 0.0191 |     160 B |        0.29 |
| CharArraySecure     | Secure     | 14     | 1,170.42 ns |  4.963 ns |  4.643 ns | 1,171.53 ns |  0.83 |    0.01 | 0.0134 |     112 B |        0.20 |
|                     |            |        |             |           |           |             |       |         |        |           |             |
| **SecureRandom**        | **Secure**     | **24**     | **2,278.24 ns** | **13.348 ns** | **11.833 ns** | **2,277.78 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| StringBuilderSecure | Secure     | 24     | 2,000.61 ns |  8.895 ns |  7.428 ns | 2,002.13 ns |  0.88 |    0.01 | 0.0229 |     192 B |        0.16 |
| CharArraySecure     | Secure     | 24     | 1,983.18 ns |  8.655 ns |  7.672 ns | 1,981.64 ns |  0.87 |    0.01 | 0.0153 |     144 B |        0.12 |
|                     |            |        |             |           |           |             |       |         |        |           |             |
| **SecureRandom**        | **Secure**     | **32**     | **3,158.97 ns** | **14.740 ns** | **12.308 ns** | **3,157.65 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| StringBuilderSecure | Secure     | 32     | 2,636.48 ns | 14.115 ns | 13.203 ns | 2,638.96 ns |  0.83 |    0.01 | 0.0267 |     224 B |        0.12 |
| CharArraySecure     | Secure     | 32     | 2,629.67 ns | 11.391 ns | 10.655 ns | 2,630.99 ns |  0.83 |    0.00 | 0.0191 |     176 B |        0.09 |
|                     |            |        |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **14**     |   **218.57 ns** |  **3.956 ns** |  **3.700 ns** |   **217.32 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| StringBuilder       | Vulnerable | 14     |    91.63 ns |  1.880 ns |  4.540 ns |    89.94 ns |  0.42 |    0.02 | 0.0191 |     160 B |        0.25 |
| CharArray           | Vulnerable | 14     |    66.62 ns |  0.340 ns |  0.284 ns |    66.60 ns |  0.30 |    0.01 | 0.0134 |     112 B |        0.18 |
|                     |            |        |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **24**     |   **309.67 ns** |  **5.906 ns** |  **5.525 ns** |   **306.79 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| StringBuilder       | Vulnerable | 24     |   158.18 ns |  3.241 ns |  8.706 ns |   157.86 ns |  0.51 |    0.03 | 0.0229 |     192 B |        0.15 |
| CharArray           | Vulnerable | 24     |   108.31 ns |  1.156 ns |  1.082 ns |   107.71 ns |  0.35 |    0.01 | 0.0172 |     144 B |        0.11 |
|                     |            |        |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **32**     |   **384.21 ns** |  **4.711 ns** |  **3.678 ns** |   **385.03 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| StringBuilder       | Vulnerable | 32     |   182.13 ns |  2.545 ns |  2.125 ns |   182.57 ns |  0.47 |    0.01 | 0.0267 |     224 B |        0.12 |
| CharArray           | Vulnerable | 32     |   141.43 ns |  0.751 ns |  0.627 ns |   141.33 ns |  0.37 |    0.00 | 0.0210 |     176 B |        0.09 |


Okay, that's another modest improvement, and we've confirmed the `char[]` approach beats out `StringBuilder` for building short strings from characters.


## Faster random generators

The time in our secure version is completely dominated by `GetInt32`, we can improve performance by getting all the bytes we need at once and then encoding them into characters.

To do so will mean making an important compromise so we don't introduce bias. 

Our character set is 74 characters, meaning if we were to generate 1 byte and then do `value % 74`, we would be introducing bias toward characters `a` through `H` as can be seen by running this code:

```csharp
byte[] foo = new byte[1024*1024];
string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

Random.Shared.NextBytes(foo);

var frequencies = foo.Select(f => f % 74)
.GroupBy(f => f)
.OrderBy(f => f.Key)
.Select(f => new { Value = characters[f.Key], Frequency = f.Count() });

foreach (var c in frequencies) {
 	Console.WriteLine($"'{c.Value}': {c.Frequency}");
}
```

```output
'a': 16452
'b': 16693
...
'G': 16527
'H': 16543
'I': 12233
'J': 12248
'K': 12242
...
'_': 12279
'+': 12203
```
There's a heavy bias with `a` through `H` having approximately ~16,300 appearances with `I` through `+` having roughly 12,200.

To eliminate this bias, we need a character set that divides evenly into 256. We can either try to pad to 128 characters, which will significantly increase the entropy of our password, or cut down to 64 characters, which will have the unfortunate side-effect of also reducing entropy.

We'll cut down to 64, because it's difficult to think of 50 more recognisable characters, and we can also take the opportunity to cut out characters that can be confused for each other in some fonts, such as I and l. 

The reduction in entropy for a 16 character password is going from `74^16  ~= 100 bits`, to `64^16 = 96 bits`.  So we've lost around 4 bits from our password. If this is a concern, then we can increase our password length by one character to accomodate.

It is a weakening, but as long as it's properly documented, should not be a concern.

Let's define our new character set:


```csharp
private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";
```

Now we have a character set that won't introduce bias, let's add a function that gets all the bytes at once from our Random sources:

```csharp
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
```

With of course an equivalent "Secure" version using `RandomNumberGenerator.Fill`.

Results:
| Method           | Categories | Length | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------- |------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**     | **Secure**     | **14**     | **1,385.36 ns** |  **7.762 ns** |  **6.881 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| BufferSecure     | Secure     | 14     |    86.59 ns |  0.738 ns |  0.654 ns |  0.06 |    0.00 | 0.0181 |     152 B |        0.27 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **24**     | **2,385.88 ns** | **15.122 ns** | **13.405 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| BufferSecure     | Secure     | 24     |   107.46 ns |  1.057 ns |  0.989 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **32**     | **3,182.64 ns** | **13.813 ns** | **10.785 ns** |  **1.00** |    **0.00** | **0.2213** |    **1856 B** |        **1.00** |
| BufferSecure     | Secure     | 32     |   117.98 ns |  1.035 ns |  0.917 ns |  0.04 |    0.00 | 0.0277 |     232 B |        0.12 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **14**     |   **218.01 ns** |  **3.323 ns** |  **3.412 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| Buffer           | Vulnerable | 14     |    32.63 ns |  0.345 ns |  0.269 ns |  0.15 |    0.00 | 0.0181 |     152 B |        0.24 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **24**     |   **306.43 ns** |  **4.712 ns** |  **4.177 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| Buffer           | Vulnerable | 24     |    37.25 ns |  0.270 ns |  0.225 ns |  0.12 |    0.00 | 0.0229 |     192 B |        0.15 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **32**     |   **374.71 ns** |  **4.076 ns** |  **3.812 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| Buffer           | Vulnerable | 32     |    44.37 ns |  0.522 ns |  0.488 ns |  0.12 |    0.00 | 0.0277 |     232 B |        0.12 |


Now there's the improvement we were hoping for! Our Vulnerable version is now up to 8 times faster than the original co-pilot output.

More importantly, our secure version is actually faster than the original vulnerable version. 

We've sacrificed some generation strength to achieve this, can we maintain this speed while also using our full character set?  We actually might. While looking up `RandomNumberGenerator.Fill` I spotted `RandomNumberGenerator.GetItems<T>`, which:

> Creates an array populated with items chosen at random from choices

That sounds exactly like what we're after. There's also `Random.Shared.GetItems<T>`, let's implement them, going back to our original 74 character set. This leaves our methods as:

```csharp
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
```

That's definitely a lot neater code than the original, let's see how it performs:

| Method           | Categories | Length | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------- |------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**     | **Secure**     | **14**     | **1,375.0 ns** |  **8.36 ns** |  **7.41 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| GetItemsSecure   | Secure     | 14     |   208.9 ns |  1.95 ns |  1.73 ns |  0.15 |    0.00 | 0.0134 |     112 B |        0.20 |
|                  |            |        |            |          |          |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **24**     | **2,405.9 ns** | **32.45 ns** | **30.35 ns** |  **1.00** |    **0.02** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsSecure   | Secure     | 24     |   302.6 ns |  1.86 ns |  1.55 ns |  0.13 |    0.00 | 0.0172 |     144 B |        0.12 |
|                  |            |        |            |          |          |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **32**     | **3,184.5 ns** | **22.23 ns** | **19.71 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| GetItemsSecure   | Secure     | 32     |   369.4 ns |  3.19 ns |  2.49 ns |  0.12 |    0.00 | 0.0210 |     176 B |        0.09 |
|                  |            |        |            |          |          |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **14**     |   **217.4 ns** |  **2.64 ns** |  **2.34 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| GetItems         | Vulnerable | 14     |   125.0 ns |  1.46 ns |  1.30 ns |  0.57 |    0.01 | 0.0134 |     112 B |        0.18 |
|                  |            |        |            |          |          |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **24**     |   **309.8 ns** |  **4.09 ns** |  **3.63 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| GetItems         | Vulnerable | 24     |   187.3 ns |  1.67 ns |  1.48 ns |  0.60 |    0.01 | 0.0172 |     144 B |        0.11 |
|                  |            |        |            |          |          |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **32**     |   **383.6 ns** |  **5.39 ns** |  **4.50 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| GetItems         | Vulnerable | 32     |   243.7 ns |  2.80 ns |  2.62 ns |  0.64 |    0.01 | 0.0210 |     176 B |        0.09 |

Right, so that's a bit of a performance regression, so we'd need to understand the trade-offs of using the full character set vs the reduced character set with better performance.

## Fixing the functionality

It's important we don't lose sight of our goal, a working password generator that won't frustrate us 1 in 100 times we use it by returning something with no symbols.

There are a few approaches to this, some of which are fraught with the danger of re-introducing bias. One naive way would be to simply generate a character (or `k` characters ) from the symbols set, then generate `n-k` characters from the full set, then shuffle them all together. This would however reintroduce a subtle bias.

The easiest way to demonstrate this bias is with a set of 3 fair coins.
We have 8 possible random outcomes:

```
HHH
HHT
HTH
HTT
THH
THT
TTH
TTT
```

 If we demand we "must have at least 1 tails", then we have 7 possible equally likely outcomes, everything except `HHH`.

 If we try to generate this by picking a `T`, then randomly picking the other two, and then shuffling, we have 4 intial outcomes:

```
T + HH
T + HT
T + TH
T + TT
```

When shuffled, become 24 equally likely outcomes:

```
( From T + HH )

THH
THH
HTH
HHT
HTH
HHT

( From T + HT )
THT
TTH
HTT
HTT
THT
TTH

( From T + TH )

THT
TTH
HTT
HTT
THT
TTH

( From T + TT )
TTT
TTT
TTT
TTT
TTT
TTT

```

Yes, 6 in 24, a full quarter of all outcomes are `TTT`, yet with a fair coin it should be 1 in 7 of cases where there is at least one `T`.


So that approach is out. The fairer way to do this is to generate a password, then if it doesn't meet the criteria, throw it out entirely and start again. 

We'll parameterise the minimum number of special characters.

```csharp
[Params(0, 1, 2)]
public int MinmumSpecialCharacters { get; set; }
```

In the case where we're first generating `byte[]` with our character set, we've arranged our character set so we can cheaply determine if there is a special character. We just have to look at the value of the byte pre-lookup to see if it is `55` or higher, since `55` to `63` are all special characters.

For the case where we're using `GetItems`, we have to count `char.IsAsciiLetterOrDigit` and take that from our password length, then compare that count against the requested minimum symbols.

```csharp
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
```
For this solution we have had to enumerate the array again for the `GetItems` approach, which we expect to further worsen it's performance relative to the 64 character set approach, so let's see the results:

<details>
<summary>Results table - Rejection Sampling (Example5.cs)</summary>

| Method                      | Categories | MinmumSpecialCharacters | Length | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------- |------------------------ |------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**                | **Secure**     | **0**                       | **14**     | **1,280.72 ns** | **11.152 ns** | **10.432 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 0                       | 14     |    95.00 ns |  0.904 ns |  0.801 ns |  0.07 |    0.00 | 0.0181 |     152 B |        0.27 |
| GetItemsWithRejectionSecure | Secure     | 0                       | 14     |   271.84 ns |  1.882 ns |  1.761 ns |  0.21 |    0.00 | 0.0134 |     112 B |        0.20 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **0**                       | **24**     | **2,389.22 ns** | **15.205 ns** | **14.223 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 0                       | 24     |   124.85 ns |  1.135 ns |  0.948 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
| GetItemsWithRejectionSecure | Secure     | 0                       | 24     |   392.17 ns |  1.893 ns |  1.580 ns |  0.16 |    0.00 | 0.0172 |     144 B |        0.12 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **0**                       | **32**     | **3,189.27 ns** | **17.296 ns** | **15.332 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 0                       | 32     |   141.00 ns |  2.724 ns |  2.415 ns |  0.04 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejectionSecure | Secure     | 0                       | 32     |   502.59 ns |  4.541 ns |  4.248 ns |  0.16 |    0.00 | 0.0210 |     176 B |        0.09 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **14**     | **1,339.70 ns** |  **9.988 ns** |  **9.343 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 1                       | 14     |   100.03 ns |  0.954 ns |  0.797 ns |  0.07 |    0.00 | 0.0181 |     152 B |        0.27 |
| GetItemsWithRejectionSecure | Secure     | 1                       | 14     |   306.75 ns |  2.379 ns |  2.225 ns |  0.23 |    0.00 | 0.0138 |     117 B |        0.21 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **24**     | **2,312.51 ns** | **19.275 ns** | **18.030 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 1                       | 24     |   122.86 ns |  1.833 ns |  1.530 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
| GetItemsWithRejectionSecure | Secure     | 1                       | 24     |   418.61 ns |  3.800 ns |  3.554 ns |  0.18 |    0.00 | 0.0172 |     145 B |        0.12 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **32**     | **3,115.85 ns** | **31.016 ns** | **29.013 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 1                       | 32     |   145.89 ns |  2.309 ns |  2.047 ns |  0.05 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejectionSecure | Secure     | 1                       | 32     |   513.57 ns |  4.516 ns |  4.224 ns |  0.16 |    0.00 | 0.0210 |     176 B |        0.09 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **14**     | **1,327.21 ns** | **11.720 ns** | **10.963 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 2                       | 14     |   105.35 ns |  1.197 ns |  1.120 ns |  0.08 |    0.00 | 0.0181 |     152 B |        0.27 |
| GetItemsWithRejectionSecure | Secure     | 2                       | 14     |   406.77 ns |  3.757 ns |  3.514 ns |  0.31 |    0.00 | 0.0162 |     137 B |        0.24 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **24**     | **2,228.90 ns** | **27.512 ns** | **25.735 ns** |  **1.00** |    **0.02** | **0.1411** |    **1200 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 2                       | 24     |   130.80 ns |  1.277 ns |  1.194 ns |  0.06 |    0.00 | 0.0229 |     192 B |        0.16 |
| GetItemsWithRejectionSecure | Secure     | 2                       | 24     |   443.67 ns |  4.374 ns |  4.092 ns |  0.20 |    0.00 | 0.0176 |     150 B |        0.12 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **32**     | **3,051.03 ns** | **18.090 ns** | **15.106 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 2                       | 32     |   135.78 ns |  1.685 ns |  1.407 ns |  0.04 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejectionSecure | Secure     | 2                       | 32     |   533.64 ns |  6.711 ns |  6.278 ns |  0.17 |    0.00 | 0.0210 |     178 B |        0.10 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **14**     |   **215.75 ns** |  **3.378 ns** |  **3.159 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| RejectionSample             | Vulnerable | 0                       | 14     |    44.56 ns |  0.945 ns |  0.928 ns |  0.21 |    0.01 | 0.0181 |     152 B |        0.24 |
| GetItemsWithRejection       | Vulnerable | 0                       | 14     |   189.10 ns |  1.673 ns |  1.565 ns |  0.88 |    0.01 | 0.0134 |     112 B |        0.18 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **24**     |   **308.09 ns** |  **4.979 ns** |  **4.158 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| RejectionSample             | Vulnerable | 0                       | 24     |    49.44 ns |  0.416 ns |  0.347 ns |  0.16 |    0.00 | 0.0229 |     192 B |        0.15 |
| GetItemsWithRejection       | Vulnerable | 0                       | 24     |   287.98 ns |  2.550 ns |  2.385 ns |  0.93 |    0.01 | 0.0172 |     144 B |        0.11 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **32**     |   **380.12 ns** |  **5.595 ns** |  **4.672 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| RejectionSample             | Vulnerable | 0                       | 32     |    55.34 ns |  0.588 ns |  0.491 ns |  0.15 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejection       | Vulnerable | 0                       | 32     |   375.89 ns |  3.337 ns |  3.121 ns |  0.99 |    0.01 | 0.0210 |     176 B |        0.09 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **14**     |   **211.37 ns** |  **2.599 ns** |  **2.170 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| RejectionSample             | Vulnerable | 1                       | 14     |    36.02 ns |  0.462 ns |  0.360 ns |  0.17 |    0.00 | 0.0181 |     152 B |        0.24 |
| GetItemsWithRejection       | Vulnerable | 1                       | 14     |   206.96 ns |  1.045 ns |  0.873 ns |  0.98 |    0.01 | 0.0138 |     117 B |        0.19 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **24**     |   **305.28 ns** |  **2.072 ns** |  **1.618 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| RejectionSample             | Vulnerable | 1                       | 24     |    42.89 ns |  0.622 ns |  0.582 ns |  0.14 |    0.00 | 0.0229 |     192 B |        0.15 |
| GetItemsWithRejection       | Vulnerable | 1                       | 24     |   301.15 ns |  1.185 ns |  0.925 ns |  0.99 |    0.01 | 0.0172 |     145 B |        0.11 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **32**     |   **382.83 ns** |  **7.717 ns** |  **6.444 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| RejectionSample             | Vulnerable | 1                       | 32     |    49.15 ns |  0.482 ns |  0.402 ns |  0.13 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejection       | Vulnerable | 1                       | 32     |   372.54 ns |  1.795 ns |  1.499 ns |  0.97 |    0.02 | 0.0210 |     176 B |        0.09 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **14**     |   **213.71 ns** |  **4.080 ns** |  **3.617 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| RejectionSample             | Vulnerable | 2                       | 14     |    47.86 ns |  1.007 ns |  2.057 ns |  0.22 |    0.01 | 0.0181 |     152 B |        0.24 |
| GetItemsWithRejection       | Vulnerable | 2                       | 14     |   272.85 ns |  2.105 ns |  1.866 ns |  1.28 |    0.02 | 0.0162 |     137 B |        0.22 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **24**     |   **304.27 ns** |  **4.919 ns** |  **4.108 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| RejectionSample             | Vulnerable | 2                       | 24     |    54.69 ns |  0.295 ns |  0.246 ns |  0.18 |    0.00 | 0.0229 |     192 B |        0.15 |
| GetItemsWithRejection       | Vulnerable | 2                       | 24     |   317.80 ns |  2.816 ns |  2.634 ns |  1.04 |    0.02 | 0.0176 |     150 B |        0.12 |
|                             |            |                         |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **32**     |   **384.55 ns** |  **7.328 ns** |  **7.525 ns** |  **1.00** |    **0.03** | **0.2303** |    **1928 B** |        **1.00** |
| RejectionSample             | Vulnerable | 2                       | 32     |    61.80 ns |  1.081 ns |  0.958 ns |  0.16 |    0.00 | 0.0277 |     232 B |        0.12 |
| GetItemsWithRejection       | Vulnerable | 2                       | 32     |   385.51 ns |  3.140 ns |  2.937 ns |  1.00 |    0.02 | 0.0210 |     178 B |        0.09 |

</details>

As expected, this has added overhead, particularly `GetItemsWithRejection`, which is left in a strange spot of being neither secure, nor particularly fast. If security is not an issue, then `RejectionSample` still performs decently well. If security is desired, then there is a choice between `RejectionSampleSecure` with it's slightly reduced entropy per output character, and `GetItemsWithRejectionSecure`.

We can try to speed up the `GetItems` based methods by using a loop to count the special characters, so we can exit early when we've met our target rather than counting all special characters.

Finally, we can try to avoid a heap allocation by using `stackalloc` to allocate the span on the stack.

<details>
<summary>Results table - Loop special character checking (Example6.cs)</summary>

| Method                      | Categories | MinmumSpecialCharacters | Length | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------- |------------------------ |------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**                | **Secure**     | **0**                       | **14**     | **1,274.2 ns** |  **8.35 ns** |  **7.40 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 0                       | 14     |   278.5 ns |  2.11 ns |  1.76 ns |  0.22 |    0.00 | 0.0134 |     112 B |        0.20 |
| SpecialLoopSecure           | Secure     | 0                       | 14     |   236.5 ns |  1.76 ns |  1.56 ns |  0.19 |    0.00 | 0.0134 |     112 B |        0.20 |
| StackAllocSecure            | Secure     | 0                       | 14     |   243.7 ns |  0.66 ns |  0.58 ns |  0.19 |    0.00 | 0.0067 |      56 B |        0.10 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **0**                       | **24**     | **2,400.5 ns** | **19.68 ns** | **18.41 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 0                       | 24     |   405.0 ns |  2.40 ns |  2.13 ns |  0.17 |    0.00 | 0.0172 |     144 B |        0.12 |
| SpecialLoopSecure           | Secure     | 0                       | 24     |   309.7 ns |  2.25 ns |  1.99 ns |  0.13 |    0.00 | 0.0172 |     144 B |        0.12 |
| StackAllocSecure            | Secure     | 0                       | 24     |   312.9 ns |  1.88 ns |  1.66 ns |  0.13 |    0.00 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **0**                       | **32**     | **2,951.6 ns** | **22.31 ns** | **20.87 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 0                       | 32     |   503.5 ns |  5.61 ns |  4.97 ns |  0.17 |    0.00 | 0.0210 |     176 B |        0.09 |
| SpecialLoopSecure           | Secure     | 0                       | 32     |   374.2 ns |  3.50 ns |  3.27 ns |  0.13 |    0.00 | 0.0210 |     176 B |        0.09 |
| StackAllocSecure            | Secure     | 0                       | 32     |   381.1 ns |  2.01 ns |  1.88 ns |  0.13 |    0.00 | 0.0105 |      88 B |        0.05 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **14**     | **1,332.3 ns** | **11.45 ns** | **10.15 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 1                       | 14     |   310.7 ns |  2.05 ns |  1.92 ns |  0.23 |    0.00 | 0.0138 |     117 B |        0.21 |
| SpecialLoopSecure           | Secure     | 1                       | 14     |   250.3 ns |  1.01 ns |  0.89 ns |  0.19 |    0.00 | 0.0134 |     112 B |        0.20 |
| StackAllocSecure            | Secure     | 1                       | 14     |   242.8 ns |  0.86 ns |  0.71 ns |  0.18 |    0.00 | 0.0067 |      56 B |        0.10 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **24**     | **2,357.4 ns** | **17.30 ns** | **15.33 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 1                       | 24     |   422.6 ns |  2.43 ns |  2.16 ns |  0.18 |    0.00 | 0.0172 |     145 B |        0.12 |
| SpecialLoopSecure           | Secure     | 1                       | 24     |   316.3 ns |  2.31 ns |  2.16 ns |  0.13 |    0.00 | 0.0172 |     144 B |        0.12 |
| StackAllocSecure            | Secure     | 1                       | 24     |   314.0 ns |  1.96 ns |  1.84 ns |  0.13 |    0.00 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **1**                       | **32**     | **3,052.0 ns** | **25.36 ns** | **23.72 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 1                       | 32     |   520.7 ns |  3.96 ns |  3.70 ns |  0.17 |    0.00 | 0.0210 |     176 B |        0.09 |
| SpecialLoopSecure           | Secure     | 1                       | 32     |   392.6 ns |  3.38 ns |  3.00 ns |  0.13 |    0.00 | 0.0210 |     176 B |        0.09 |
| StackAllocSecure            | Secure     | 1                       | 32     |   379.2 ns |  2.86 ns |  2.67 ns |  0.12 |    0.00 | 0.0105 |      88 B |        0.05 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **14**     | **1,358.7 ns** |  **9.64 ns** |  **9.01 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 2                       | 14     |   409.1 ns |  5.90 ns |  5.23 ns |  0.30 |    0.00 | 0.0162 |     137 B |        0.24 |
| SpecialLoopSecure           | Secure     | 2                       | 14     |   336.9 ns |  4.58 ns |  4.06 ns |  0.25 |    0.00 | 0.0134 |     112 B |        0.20 |
| StackAllocSecure            | Secure     | 2                       | 14     |   335.1 ns |  0.71 ns |  0.55 ns |  0.25 |    0.00 | 0.0067 |      56 B |        0.10 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **24**     | **2,343.5 ns** |  **9.81 ns** |  **8.70 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 2                       | 24     |   450.3 ns |  4.45 ns |  4.17 ns |  0.19 |    0.00 | 0.0176 |     150 B |        0.12 |
| SpecialLoopSecure           | Secure     | 2                       | 24     |   356.6 ns |  1.64 ns |  1.37 ns |  0.15 |    0.00 | 0.0172 |     144 B |        0.12 |
| StackAllocSecure            | Secure     | 2                       | 24     |   368.4 ns |  2.41 ns |  2.01 ns |  0.16 |    0.00 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **SecureRandom**                | **Secure**     | **2**                       | **32**     | **3,170.9 ns** | **48.96 ns** | **45.80 ns** |  **1.00** |    **0.02** | **0.2213** |    **1856 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 2                       | 32     |   531.8 ns | 10.52 ns | 11.26 ns |  0.17 |    0.00 | 0.0210 |     178 B |        0.10 |
| SpecialLoopSecure           | Secure     | 2                       | 32     |   424.9 ns |  5.10 ns |  4.77 ns |  0.13 |    0.00 | 0.0210 |     176 B |        0.09 |
| StackAllocSecure            | Secure     | 2                       | 32     |   406.0 ns |  2.04 ns |  1.81 ns |  0.13 |    0.00 | 0.0105 |      88 B |        0.05 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **14**     |   **217.2 ns** |  **3.71 ns** |  **3.47 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 0                       | 14     |   192.6 ns |  1.14 ns |  0.95 ns |  0.89 |    0.01 | 0.0134 |     112 B |        0.18 |
| SpecialLoop                 | Vulnerable | 0                       | 14     |   150.2 ns |  2.27 ns |  2.12 ns |  0.69 |    0.01 | 0.0134 |     112 B |        0.18 |
| StackAlloc                  | Vulnerable | 0                       | 14     |   144.7 ns |  1.21 ns |  1.13 ns |  0.67 |    0.01 | 0.0067 |      56 B |        0.09 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **24**     |   **306.7 ns** |  **4.69 ns** |  **4.16 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 0                       | 24     |   291.2 ns |  2.72 ns |  2.54 ns |  0.95 |    0.01 | 0.0172 |     144 B |        0.11 |
| SpecialLoop                 | Vulnerable | 0                       | 24     |   205.8 ns |  0.92 ns |  0.77 ns |  0.67 |    0.01 | 0.0172 |     144 B |        0.11 |
| StackAlloc                  | Vulnerable | 0                       | 24     |   200.9 ns |  1.91 ns |  1.79 ns |  0.66 |    0.01 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **0**                       | **32**     |   **375.6 ns** |  **4.12 ns** |  **3.85 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 0                       | 32     |   386.8 ns |  3.88 ns |  3.63 ns |  1.03 |    0.01 | 0.0210 |     176 B |        0.09 |
| SpecialLoop                 | Vulnerable | 0                       | 32     |   257.3 ns |  2.53 ns |  2.11 ns |  0.69 |    0.01 | 0.0210 |     176 B |        0.09 |
| StackAlloc                  | Vulnerable | 0                       | 32     |   253.1 ns |  2.16 ns |  1.91 ns |  0.67 |    0.01 | 0.0105 |      88 B |        0.05 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **14**     |   **212.1 ns** |  **2.21 ns** |  **1.85 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 1                       | 14     |   208.4 ns |  1.88 ns |  1.76 ns |  0.98 |    0.01 | 0.0138 |     117 B |        0.19 |
| SpecialLoop                 | Vulnerable | 1                       | 14     |   148.0 ns |  1.53 ns |  1.36 ns |  0.70 |    0.01 | 0.0134 |     112 B |        0.18 |
| StackAlloc                  | Vulnerable | 1                       | 14     |   146.3 ns |  1.35 ns |  1.20 ns |  0.69 |    0.01 | 0.0067 |      56 B |        0.09 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **24**     |   **305.3 ns** |  **5.51 ns** |  **4.30 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 1                       | 24     |   302.8 ns |  2.79 ns |  2.47 ns |  0.99 |    0.02 | 0.0172 |     145 B |        0.11 |
| SpecialLoop                 | Vulnerable | 1                       | 24     |   203.7 ns |  1.41 ns |  1.18 ns |  0.67 |    0.01 | 0.0172 |     144 B |        0.11 |
| StackAlloc                  | Vulnerable | 1                       | 24     |   201.5 ns |  2.20 ns |  2.06 ns |  0.66 |    0.01 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **1**                       | **32**     |   **376.2 ns** |  **3.70 ns** |  **3.46 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 1                       | 32     |   365.3 ns |  2.39 ns |  1.99 ns |  0.97 |    0.01 | 0.0210 |     176 B |        0.09 |
| SpecialLoop                 | Vulnerable | 1                       | 32     |   254.2 ns |  1.24 ns |  1.03 ns |  0.68 |    0.01 | 0.0210 |     176 B |        0.09 |
| StackAlloc                  | Vulnerable | 1                       | 32     |   253.4 ns |  2.49 ns |  2.33 ns |  0.67 |    0.01 | 0.0105 |      88 B |        0.05 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **14**     |   **217.1 ns** |  **1.63 ns** |  **1.37 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 2                       | 14     |   274.6 ns |  2.58 ns |  2.28 ns |  1.27 |    0.01 | 0.0162 |     137 B |        0.22 |
| SpecialLoop                 | Vulnerable | 2                       | 14     |   207.6 ns |  1.65 ns |  1.47 ns |  0.96 |    0.01 | 0.0134 |     112 B |        0.18 |
| StackAlloc                  | Vulnerable | 2                       | 14     |   203.9 ns |  0.93 ns |  0.77 ns |  0.94 |    0.01 | 0.0067 |      56 B |        0.09 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **24**     |   **304.0 ns** |  **1.89 ns** |  **1.58 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 2                       | 24     |   320.2 ns |  2.43 ns |  2.16 ns |  1.05 |    0.01 | 0.0176 |     150 B |        0.12 |
| SpecialLoop                 | Vulnerable | 2                       | 24     |   236.8 ns |  1.78 ns |  1.67 ns |  0.78 |    0.01 | 0.0172 |     144 B |        0.11 |
| StackAlloc                  | Vulnerable | 2                       | 24     |   231.2 ns |  1.92 ns |  1.80 ns |  0.76 |    0.01 | 0.0086 |      72 B |        0.06 |
|                             |            |                         |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **2**                       | **32**     |   **375.5 ns** |  **4.20 ns** |  **3.93 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 2                       | 32     |   385.3 ns |  3.45 ns |  3.23 ns |  1.03 |    0.01 | 0.0210 |     178 B |        0.09 |
| SpecialLoop                 | Vulnerable | 2                       | 32     |   279.0 ns |  2.41 ns |  2.14 ns |  0.74 |    0.01 | 0.0210 |     176 B |        0.09 |
| StackAlloc                  | Vulnerable | 2                       | 32     |   272.8 ns |  2.34 ns |  2.19 ns |  0.73 |    0.01 | 0.0105 |      88 B |        0.05 |

</details>

Checking in a loop has significantly reduced the overhead of counting special characters. Stack allocation has shaved 1-2ns off the time too, but perhaps more importantly, has halved the heap usage.


# Notes

## Motivation

You may be thinking:

> Password generation shouldn't happen often enough that even 1ms, let alone 1µs should matter. All this optimization is a waste.

And you'd be right if the goal of this was to optimise password generation in production rather than a demonstration of the tooling with an easy to understand example.

I would say however, that performance matters, because two things happen if you let in poor quality code:

1. The code gets copied around, and used outside of its original context.
2. Your team gets used to checking in bad code, and a "Looks good to me" culture.

The first can be particularly problematic. While password generation almost certainly isn't a hot path, who knows when next time someone might need some random data. And if this code is hanging around your code-base, it can easily get copied and used elsewhere where the performance concerns are more valid. You could argue it's up to the reviewer of that feature to then catch it at that time, but I've seen worse justifed with, "Well we used this approach elsewhere" and get nodded through.

The second is more subjective, but it's a matter of pride to work on a team where the first code that co-pilot generated  would get picked apart and not accepted. A culture of rubber-stamping PRs can set in if standards aren't held up and assumptions aren't checked.

## False Optimisations

It's worth noting here some approaches that did not improve performance. I originally wanted to include them in the bulk of the post but I felt it was already getting too long, so they got cut from the final version.

 ### Replacing the character lookup `string` with `char[]`

 This actually decreased performance. `string` in c# are immutable and as such can be treated as a `ReadOnlySpan<char>` wherever needed, and are fast to index against. There was no improvement to using `char[]`, just a neglible downside.

 ### Avoiding Modulo with a longer lookup

 This one was flat. I had the idea to avoid the modulo by using a repeated string, i.e. instead of:

 ```csharp
string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";
char randomChar = characters[value % 64];
 ```

 You would do:

```csharp
string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";
char randomChar = characters[value];
 ```
 
I was hopeful for this one, but it had no appreciable effect on performance.

### Avoiding modulo with bittricks / bitshifiting
Other approaches to avoiding modulo such as bitwise `&` or bitshifting were the same performance as modulo. I didn't check whether they compiled to the same IL but I'd imagine they did. I'm not smarter than a compiler.

### String.Create
I'd heard that rather than `new string(buffer)`, there were faster methods to allocate strings with `String.Create`. I experimented with what is an awkward method and I couldn't get it close. That may be down to the very small string values I'm dealing with.

### RandomNumberGenerator.GetBytes

Instead of `RandomNumberGenerator.Fill` you can use `RandomNumberGenerator.GetBytes` to return the array. This is cleaner syntax but it has no effect on performance. I left `RandomNumberGenerator.Fill` to make the example have syntax that matched `Random.Shared.NextBytes` more closely to make it clear they were otherwise the same.

### Matching special characters with SearchValues<T>
Instead of `!char.IsAsciiLetterOrDigit`, I tried `System.Buffer.SearchValues` with a span of special characters. This added ~20ns to the runtime. ( See `Searching.cs` for code. )


## Addendum

Here's a benchmark run with all the functions, with a single baseline of the original co-pilot output. The arguments were changed to properties to support cleaner parameterisation within BenchmarkDotNet and a shared common baseline set.

<details>

<summary>Full results comparison</summary>

These results can be recreated by running the program in this repository.

```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3800X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.2.25164.34
  [Host]     : .NET 10.0.0 (10.0.25.16302), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.0 (10.0.25.16302), X64 RyuJIT AVX2


```
| Method                       | Categories | Length | MinmumSpecialCharacters | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |----------- |------- |------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| **GeneratePassword**             | ****           | **14**     | **0**                       |   **215.75 ns** |  **3.793 ns** |  **3.548 ns** |   **214.55 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 0                       | 1,385.24 ns |  8.990 ns |  7.969 ns | 1,385.91 ns |  6.42 |    0.11 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 0                       |   129.72 ns |  1.398 ns |  1.167 ns |   129.88 ns |  0.60 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **14**     | **1**                       |   **210.58 ns** |  **2.404 ns** |  **2.007 ns** |   **210.05 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 1                       | 1,340.80 ns | 23.188 ns | 30.151 ns | 1,329.52 ns |  6.37 |    0.15 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 1                       |   128.65 ns |  1.271 ns |  0.992 ns |   128.77 ns |  0.61 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **14**     | **2**                       |   **211.72 ns** |  **4.151 ns** |  **4.077 ns** |   **209.72 ns** |  **1.00** |    **0.03** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 2                       | 1,359.95 ns | 11.902 ns | 11.133 ns | 1,356.93 ns |  6.43 |    0.13 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 2                       |   127.48 ns |  1.065 ns |  0.832 ns |   127.52 ns |  0.60 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **0**                       |   **305.68 ns** |  **5.666 ns** |  **5.300 ns** |   **305.30 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 0                       | 2,395.31 ns | 46.895 ns | 55.825 ns | 2,370.40 ns |  7.84 |    0.22 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 0                       |   229.69 ns |  3.249 ns |  2.536 ns |   229.95 ns |  0.75 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **1**                       |   **305.93 ns** |  **3.580 ns** |  **3.174 ns** |   **306.07 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 1                       | 2,287.34 ns | 19.272 ns | 17.084 ns | 2,283.84 ns |  7.48 |    0.09 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 1                       |   227.92 ns |  4.142 ns |  3.459 ns |   227.26 ns |  0.75 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **2**                       |   **301.40 ns** |  **3.863 ns** |  **3.016 ns** |   **301.27 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 2                       | 2,323.11 ns |  9.154 ns |  7.644 ns | 2,322.33 ns |  7.71 |    0.08 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 2                       |   226.98 ns |  4.383 ns |  3.660 ns |   226.45 ns |  0.75 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **0**                       |   **374.21 ns** |  **4.377 ns** |  **4.095 ns** |   **372.42 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 0                       | 2,940.00 ns | 20.856 ns | 18.488 ns | 2,944.29 ns |  7.86 |    0.10 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 0                       |   315.04 ns |  2.574 ns |  2.009 ns |   315.06 ns |  0.84 |    0.01 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **1**                       |   **378.56 ns** |  **6.198 ns** |  **5.176 ns** |   **378.47 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 1                       | 3,107.14 ns | 28.586 ns | 25.341 ns | 3,101.47 ns |  8.21 |    0.13 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 1                       |   304.02 ns |  1.081 ns |  0.844 ns |   304.00 ns |  0.80 |    0.01 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **2**                       |   **390.50 ns** |  **6.386 ns** |  **5.974 ns** |   **388.73 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 2                       | 3,128.55 ns | 30.199 ns | 28.248 ns | 3,127.64 ns |  8.01 |    0.14 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 2                       |   310.85 ns |  4.786 ns |  3.996 ns |   310.55 ns |  0.80 |    0.02 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **0**                       | **1,185.09 ns** | **19.311 ns** | **18.063 ns** | **1,174.75 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 0                       | 1,158.14 ns |  4.036 ns |  3.776 ns | 1,158.91 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 0                       |    85.71 ns |  0.503 ns |  0.420 ns |    85.58 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 0                       |   212.04 ns |  2.828 ns |  2.646 ns |   210.90 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 0                       |    98.66 ns |  0.955 ns |  0.893 ns |    98.63 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 0                       |   281.52 ns |  1.200 ns |  1.002 ns |   281.65 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| SpecialLoopSecure            | Secure     | 14     | 0                       |   239.94 ns |  1.372 ns |  1.146 ns |   239.51 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAllocSecure             | Secure     | 14     | 0                       |   245.24 ns |  1.685 ns |  1.577 ns |   245.40 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **1**                       | **1,188.01 ns** |  **9.011 ns** |  **7.525 ns** | **1,187.19 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 1                       | 1,161.76 ns |  5.315 ns |  4.712 ns | 1,160.91 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 1                       |    86.07 ns |  0.390 ns |  0.326 ns |    86.01 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 1                       |   212.12 ns |  2.252 ns |  2.107 ns |   211.24 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 1                       |   101.42 ns |  0.613 ns |  0.512 ns |   101.26 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 1                       |   300.96 ns |  2.228 ns |  2.084 ns |   300.47 ns |     ? |       ? | 0.0138 |     117 B |           ? |
| SpecialLoopSecure            | Secure     | 14     | 1                       |   230.91 ns |  1.113 ns |  0.929 ns |   230.93 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAllocSecure             | Secure     | 14     | 1                       |   242.83 ns |  2.003 ns |  1.874 ns |   242.11 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **2**                       | **1,182.45 ns** |  **4.747 ns** |  **4.440 ns** | **1,183.29 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 2                       | 1,170.14 ns |  6.867 ns |  6.424 ns | 1,170.44 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 2                       |    84.92 ns |  0.628 ns |  0.524 ns |    84.64 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 2                       |   209.01 ns |  1.692 ns |  1.583 ns |   208.98 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 2                       |   106.88 ns |  0.855 ns |  0.800 ns |   106.71 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 2                       |   386.26 ns |  2.061 ns |  1.721 ns |   386.12 ns |     ? |       ? | 0.0162 |     137 B |           ? |
| SpecialLoopSecure            | Secure     | 14     | 2                       |   325.62 ns |  1.919 ns |  1.795 ns |   325.56 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAllocSecure             | Secure     | 14     | 2                       |   341.45 ns |  2.575 ns |  2.408 ns |   341.46 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **0**                       | **1,986.09 ns** |  **7.470 ns** |  **6.987 ns** | **1,990.54 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 0                       | 1,973.83 ns |  7.796 ns |  7.292 ns | 1,975.28 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 0                       |   108.45 ns |  1.711 ns |  1.601 ns |   108.34 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 0                       |   299.18 ns |  2.914 ns |  2.726 ns |   298.42 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 0                       |   119.60 ns |  0.658 ns |  0.549 ns |   119.57 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 0                       |   404.17 ns |  2.775 ns |  2.596 ns |   404.11 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| SpecialLoopSecure            | Secure     | 24     | 0                       |   312.67 ns |  1.939 ns |  1.619 ns |   312.76 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAllocSecure             | Secure     | 24     | 0                       |   322.41 ns |  1.980 ns |  1.852 ns |   322.38 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **1**                       | **1,989.94 ns** |  **9.423 ns** |  **8.353 ns** | **1,990.50 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 1                       | 2,159.30 ns |  8.801 ns |  7.802 ns | 2,159.61 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 1                       |   107.50 ns |  1.434 ns |  1.341 ns |   107.10 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 1                       |   293.72 ns |  2.800 ns |  2.619 ns |   292.97 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 1                       |   125.20 ns |  1.823 ns |  1.705 ns |   124.90 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 1                       |   416.30 ns |  2.899 ns |  2.570 ns |   415.79 ns |     ? |       ? | 0.0172 |     145 B |           ? |
| SpecialLoopSecure            | Secure     | 24     | 1                       |   318.78 ns |  2.812 ns |  2.630 ns |   318.29 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAllocSecure             | Secure     | 24     | 1                       |   312.46 ns |  1.519 ns |  1.269 ns |   312.75 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **2**                       | **1,990.07 ns** |  **6.972 ns** |  **6.180 ns** | **1,989.67 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 2                       | 1,985.53 ns | 11.634 ns | 10.883 ns | 1,983.66 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 2                       |   108.31 ns |  1.357 ns |  1.203 ns |   107.83 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 2                       |   305.47 ns |  3.098 ns |  2.898 ns |   305.27 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 2                       |   133.99 ns |  1.627 ns |  1.442 ns |   133.84 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 2                       |   445.34 ns |  3.244 ns |  3.035 ns |   446.11 ns |     ? |       ? | 0.0176 |     150 B |           ? |
| SpecialLoopSecure            | Secure     | 24     | 2                       |   361.00 ns |  3.072 ns |  2.874 ns |   360.94 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAllocSecure             | Secure     | 24     | 2                       |   362.64 ns |  2.274 ns |  2.127 ns |   362.30 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **0**                       | **2,663.43 ns** | **14.367 ns** | **13.439 ns** | **2,662.94 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 0                       | 2,635.60 ns | 10.638 ns |  9.951 ns | 2,634.98 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 0                       |   120.60 ns |  0.777 ns |  0.689 ns |   120.71 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 0                       |   365.84 ns |  6.409 ns |  5.995 ns |   364.31 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 0                       |   156.50 ns |  0.690 ns |  0.539 ns |   156.52 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 0                       |   499.75 ns |  3.139 ns |  2.782 ns |   499.69 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| SpecialLoopSecure            | Secure     | 32     | 0                       |   398.02 ns |  3.087 ns |  2.887 ns |   398.12 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAllocSecure             | Secure     | 32     | 0                       |   390.82 ns |  3.490 ns |  3.264 ns |   389.70 ns |     ? |       ? | 0.0105 |      88 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **1**                       | **2,638.28 ns** | **13.093 ns** | **12.247 ns** | **2,634.78 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 1                       | 2,623.61 ns | 10.045 ns |  9.396 ns | 2,620.10 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 1                       |   117.34 ns |  0.484 ns |  0.405 ns |   117.34 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 1                       |   372.87 ns |  3.183 ns |  2.977 ns |   372.13 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 1                       |   151.72 ns |  2.136 ns |  1.893 ns |   151.65 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 1                       |   504.52 ns |  4.325 ns |  4.045 ns |   504.67 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| SpecialLoopSecure            | Secure     | 32     | 1                       |   386.11 ns |  2.910 ns |  2.722 ns |   386.58 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAllocSecure             | Secure     | 32     | 1                       |   376.84 ns |  2.314 ns |  2.165 ns |   376.26 ns |     ? |       ? | 0.0105 |      88 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **2**                       | **2,635.03 ns** |  **8.654 ns** |  **8.095 ns** | **2,636.25 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 2                       | 2,615.32 ns | 10.481 ns |  9.804 ns | 2,614.03 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 2                       |   116.30 ns |  1.251 ns |  1.044 ns |   116.03 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 2                       |   370.29 ns |  3.049 ns |  2.546 ns |   370.88 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 2                       |   157.19 ns |  2.703 ns |  2.257 ns |   156.89 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 2                       |   518.26 ns |  3.547 ns |  3.318 ns |   517.21 ns |     ? |       ? | 0.0210 |     178 B |           ? |
| SpecialLoopSecure            | Secure     | 32     | 2                       |   401.38 ns |  3.397 ns |  3.178 ns |   400.96 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAllocSecure             | Secure     | 32     | 2                       |   400.59 ns |  3.066 ns |  2.867 ns |   400.15 ns |     ? |       ? | 0.0105 |      88 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **0**                       |   **103.71 ns** |  **2.085 ns** |  **2.230 ns** |   **103.56 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 0                       |    66.52 ns |  0.477 ns |  0.423 ns |    66.42 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 0                       |    34.93 ns |  0.725 ns |  0.678 ns |    34.65 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 0                       |   125.68 ns |  0.874 ns |  0.730 ns |   125.64 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 0                       |    37.67 ns |  0.467 ns |  0.414 ns |    37.53 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 0                       |   184.69 ns |  1.629 ns |  1.524 ns |   184.28 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| SpecialLoop                  | Vulnerable | 14     | 0                       |   148.74 ns |  0.908 ns |  0.709 ns |   149.01 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAlloc                   | Vulnerable | 14     | 0                       |   145.10 ns |  0.831 ns |  0.694 ns |   145.13 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **1**                       |   **100.21 ns** |  **1.307 ns** |  **1.159 ns** |   **100.23 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 1                       |    66.53 ns |  0.279 ns |  0.218 ns |    66.54 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 1                       |    35.07 ns |  0.408 ns |  0.341 ns |    35.09 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 1                       |   124.14 ns |  1.444 ns |  1.280 ns |   123.71 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 1                       |    36.73 ns |  0.534 ns |  0.446 ns |    36.82 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 1                       |   206.37 ns |  0.633 ns |  0.494 ns |   206.34 ns |     ? |       ? | 0.0138 |     117 B |           ? |
| SpecialLoop                  | Vulnerable | 14     | 1                       |   149.17 ns |  2.276 ns |  2.129 ns |   148.53 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAlloc                   | Vulnerable | 14     | 1                       |   145.35 ns |  0.719 ns |  0.600 ns |   145.47 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **2**                       |   **108.28 ns** |  **3.840 ns** | **11.322 ns** |   **113.80 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 2                       |    67.02 ns |  0.284 ns |  0.222 ns |    67.00 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 2                       |    38.87 ns |  0.425 ns |  0.355 ns |    38.78 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 2                       |   124.88 ns |  0.858 ns |  0.717 ns |   125.03 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 2                       |    48.20 ns |  0.666 ns |  0.590 ns |    48.02 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 2                       |   268.35 ns |  1.793 ns |  1.590 ns |   268.00 ns |     ? |       ? | 0.0162 |     137 B |           ? |
| SpecialLoop                  | Vulnerable | 14     | 2                       |   209.33 ns |  1.761 ns |  1.647 ns |   209.00 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| StackAlloc                   | Vulnerable | 14     | 2                       |   205.04 ns |  1.408 ns |  1.317 ns |   204.45 ns |     ? |       ? | 0.0067 |      56 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **0**                       |   **140.14 ns** |  **2.861 ns** |  **8.022 ns** |   **142.42 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 0                       |   107.90 ns |  0.898 ns |  0.840 ns |   107.54 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 0                       |    40.86 ns |  0.265 ns |  0.221 ns |    40.81 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 0                       |   189.76 ns |  1.358 ns |  1.204 ns |   189.70 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 0                       |    44.86 ns |  0.281 ns |  0.235 ns |    44.80 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 0                       |   286.75 ns |  2.619 ns |  2.322 ns |   286.46 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| SpecialLoop                  | Vulnerable | 24     | 0                       |   202.88 ns |  1.616 ns |  1.349 ns |   202.67 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAlloc                   | Vulnerable | 24     | 0                       |   200.00 ns |  1.594 ns |  1.413 ns |   199.66 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **1**                       |   **195.29 ns** |  **3.455 ns** |  **3.232 ns** |   **195.30 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 1                       |   108.35 ns |  0.594 ns |  0.496 ns |   108.21 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 1                       |    37.21 ns |  0.189 ns |  0.147 ns |    37.21 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 1                       |   186.75 ns |  1.421 ns |  1.329 ns |   185.99 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 1                       |    45.08 ns |  0.705 ns |  0.659 ns |    44.87 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 1                       |   302.02 ns |  1.534 ns |  1.281 ns |   302.21 ns |     ? |       ? | 0.0172 |     145 B |           ? |
| SpecialLoop                  | Vulnerable | 24     | 1                       |   203.26 ns |  1.391 ns |  1.162 ns |   203.63 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAlloc                   | Vulnerable | 24     | 1                       |   200.42 ns |  1.133 ns |  1.004 ns |   199.96 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **2**                       |   **190.69 ns** |  **3.839 ns** |  **5.628 ns** |   **191.55 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 2                       |   111.76 ns |  1.222 ns |  1.083 ns |   111.49 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 2                       |    37.13 ns |  0.167 ns |  0.139 ns |    37.12 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 2                       |   187.46 ns |  2.519 ns |  2.356 ns |   186.15 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 2                       |    56.36 ns |  0.772 ns |  0.722 ns |    55.91 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 2                       |   320.71 ns |  2.698 ns |  2.524 ns |   320.62 ns |     ? |       ? | 0.0176 |     150 B |           ? |
| SpecialLoop                  | Vulnerable | 24     | 2                       |   234.60 ns |  1.279 ns |  1.134 ns |   234.32 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAlloc                   | Vulnerable | 24     | 2                       |   230.01 ns |  2.024 ns |  1.893 ns |   229.09 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **0**                       |   **211.11 ns** |  **3.472 ns** |  **3.247 ns** |   **210.95 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 0                       |   144.00 ns |  1.119 ns |  0.934 ns |   143.89 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 0                       |    46.17 ns |  0.258 ns |  0.202 ns |    46.14 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 0                       |   244.44 ns |  2.530 ns |  2.366 ns |   244.46 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 0                       |    53.89 ns |  0.578 ns |  0.512 ns |    53.71 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 0                       |   370.80 ns |  2.476 ns |  2.316 ns |   370.08 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| SpecialLoop                  | Vulnerable | 32     | 0                       |   256.39 ns |  1.353 ns |  1.056 ns |   256.77 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAlloc                   | Vulnerable | 32     | 0                       |   253.48 ns |  1.874 ns |  1.753 ns |   253.80 ns |     ? |       ? | 0.0105 |      88 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **1**                       |   **256.33 ns** |  **3.118 ns** |  **2.764 ns** |   **256.21 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 1                       |   140.86 ns |  0.484 ns |  0.404 ns |   140.96 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 1                       |    44.40 ns |  0.480 ns |  0.449 ns |    44.24 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 1                       |   240.46 ns |  1.365 ns |  1.140 ns |   240.36 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 1                       |    53.50 ns |  0.721 ns |  0.602 ns |    53.39 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 1                       |   369.59 ns |  2.814 ns |  2.632 ns |   368.95 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| SpecialLoop                  | Vulnerable | 32     | 1                       |   256.90 ns |  1.895 ns |  1.582 ns |   257.15 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAlloc                   | Vulnerable | 32     | 1                       |   253.26 ns |  1.736 ns |  1.539 ns |   253.48 ns |     ? |       ? | 0.0105 |      88 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **2**                       |   **253.24 ns** |  **5.010 ns** |  **5.568 ns** |   **254.21 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 2                       |   144.44 ns |  1.747 ns |  1.634 ns |   144.03 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 2                       |    44.42 ns |  0.508 ns |  0.475 ns |    44.18 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 2                       |   241.91 ns |  1.967 ns |  1.744 ns |   241.59 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 2                       |    65.01 ns |  0.655 ns |  0.547 ns |    65.10 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 2                       |   385.69 ns |  2.443 ns |  2.166 ns |   385.52 ns |     ? |       ? | 0.0210 |     178 B |           ? |
| SpecialLoop                  | Vulnerable | 32     | 2                       |   276.94 ns |  2.463 ns |  2.184 ns |   276.36 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| StackAlloc                   | Vulnerable | 32     | 2                       |   271.10 ns |  0.982 ns |  0.870 ns |   271.37 ns |     ? |       ? | 0.0105 |      88 B |           ? |


</details>