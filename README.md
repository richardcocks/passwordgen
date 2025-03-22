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

I've gone with 3 different lengths of passwords, so that we can see the effect of increasing password length on generation, and also used two separate categories so we can compare our optimisation efforts on both the secure and vulnerable versions of the password generator.


```csharp
[Arguments(14)]
[Arguments(24)]
[Arguments(32)]
public string GeneratePasswordSharedRandom(int length)
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

Let's immediately look at the impact of removing `System.Random random = new System.Random();` and using the static `System.Random.Shared` for the vulnerable example.

```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3800X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.2.25164.34
  [Host]     : .NET 10.0.0 (10.0.25.16302), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.0 (10.0.25.16302), X64 RyuJIT AVX2


```
| Method                       | length | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |------- |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **GeneratePassword**             | **14**     |   **209.1 ns** |  **3.19 ns** |  **2.82 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                     | 14     | 1,355.4 ns |  6.80 ns |  6.36 ns |  6.48 |    0.09 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom | 14     |   129.4 ns |  2.12 ns |  1.66 ns |  0.62 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**             | **24**     |   **300.8 ns** |  **5.80 ns** |  **6.21 ns** |  **1.00** |    **0.03** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                     | 24     | 2,370.3 ns | 24.11 ns | 22.55 ns |  7.88 |    0.17 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom | 24     |   230.1 ns |  4.31 ns |  4.03 ns |  0.77 |    0.02 | 0.1433 |    1200 B |        0.94 |
|                              |        |            |          |          |       |         |        |           |             |
| **GeneratePassword**             | **32**     |   **375.7 ns** |  **7.42 ns** |  **9.11 ns** |  **1.00** |    **0.03** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                     | 32     | 3,149.0 ns | 16.66 ns | 14.77 ns |  8.39 |    0.20 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom | 32     |   308.8 ns |  4.07 ns |  3.40 ns |  0.82 |    0.02 | 0.2217 |    1856 B |        0.96 |


We can now demonstrably see that avoiding `new System.Random()` increased performance, roughly 30% faster for the 24 character example.

We can also see that using `RandomNumberGenerator.GetInt32` destroyed our performance, taking us into the microsecond territory and taking around 8 times as long to do the same work.

To make future comparisons easier, we can add `[BenchmarkCategory("Secure")]` and `[BenchmarkCategory("Vulnerable")]` attributes to our benchmarks to mark `GeneratePassword` and `SecureRandom` as two separate baselines, so we can more easily examine the performane impact on each. We also need to mark the class with `[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]` and `[CategoriesColumn]` to get a category column in the output table.

## String Building
Okay, let's do the other "obvious" improvement, use `StringBuilder`, so our non-secure version now looks like this:

```csharp
[BenchmarkCategory("Vulnerable"), Benchmark()]
[Arguments(14)]
[Arguments(24)]
[Arguments(32)]
public string StringBuilder(int length)
{

    string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

    StringBuilder password = new(length);
    for (int i = 0; i < length; i++)
    {
        password.Append(characters[Random.Shared.Next(characters.Length)]);
    }

    return password.ToString();
}

// With an equivalent Secure version not shown here.

```
We know the size of the string, so we were able to intialize our string builder with that capacity. But given we know the size of the string, wouldn't it be faster still to allocate a `char[]` and fill it? Let's try that at the same time and compare:

```csharp
//...Annotated as required
public string CharArray(int length)
{
    string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
    char[] buffer = new char[length];

    for (int i = 0; i < length; i++)
    {
        buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }

    return new string(buffer);
}

// With an equivalent Vulnerable version not shown here.
```



Here are the results:

| Method              | Categories | length | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------- |------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**        | **Secure**     | **14**     | **1,364.06 ns** |  **8.434 ns** |  **7.889 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| StringBuilderSecure | Secure     | 14     | 1,184.39 ns |  8.104 ns |  7.580 ns |  0.87 |    0.01 | 0.0191 |     160 B |        0.29 |
| CharArraySecure     | Secure     | 14     | 1,150.42 ns |  6.884 ns |  6.440 ns |  0.84 |    0.01 | 0.0134 |     112 B |        0.20 |
|                     |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**        | **Secure**     | **24**     | **2,377.82 ns** | **21.939 ns** | **20.522 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| StringBuilderSecure | Secure     | 24     | 2,001.11 ns | 15.455 ns | 14.457 ns |  0.84 |    0.01 | 0.0229 |     192 B |        0.16 |
| CharArraySecure     | Secure     | 24     | 1,984.42 ns | 12.424 ns | 11.621 ns |  0.83 |    0.01 | 0.0153 |     144 B |        0.12 |
|                     |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**        | **Secure**     | **32**     | **3,202.80 ns** | **24.865 ns** | **22.042 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| StringBuilderSecure | Secure     | 32     | 2,660.34 ns | 20.415 ns | 19.096 ns |  0.83 |    0.01 | 0.0267 |     224 B |        0.12 |
| CharArraySecure     | Secure     | 32     | 2,630.29 ns | 19.468 ns | 17.258 ns |  0.82 |    0.01 | 0.0191 |     176 B |        0.09 |
|                     |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **14**     |   **211.08 ns** |  **2.530 ns** |  **2.113 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| StringBuilder       | Vulnerable | 14     |    89.14 ns |  1.814 ns |  2.825 ns |  0.42 |    0.01 | 0.0191 |     160 B |        0.25 |
| CharArray           | Vulnerable | 14     |    65.74 ns |  0.775 ns |  0.725 ns |  0.31 |    0.00 | 0.0134 |     112 B |        0.18 |
|                     |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **24**     |   **303.30 ns** |  **5.931 ns** |  **4.953 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| StringBuilder       | Vulnerable | 24     |   185.86 ns |  3.759 ns |  9.770 ns |  0.61 |    0.03 | 0.0229 |     192 B |        0.15 |
| CharArray           | Vulnerable | 24     |   105.90 ns |  1.389 ns |  1.299 ns |  0.35 |    0.01 | 0.0172 |     144 B |        0.11 |
|                     |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword**    | **Vulnerable** | **32**     |   **376.81 ns** |  **6.435 ns** |  **5.374 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| StringBuilder       | Vulnerable | 32     |   204.31 ns |  4.068 ns |  5.145 ns |  0.54 |    0.02 | 0.0267 |     224 B |        0.12 |
| CharArray           | Vulnerable | 32     |   137.89 ns |  1.867 ns |  1.655 ns |  0.37 |    0.01 | 0.0210 |     176 B |        0.09 |


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

The reduction in entropy for a 16 character password is going from 74^16~100 bits, to 64^16 = 96.  So we've lost 4 bits from our password. If this is a concern, then we can increase our password length by a character to accomodate.

It is a weakening, but as long as it's properly documented, should not be a concern.

Let's define our new character set:


```csharp
string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";
```

So now we have a character set, let's add a function that gets all the bytes at once from our Random sources:

```csharp
public string Buffer(int length)
{

    string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

    byte[] bytebuffer = new byte[length];
    Random.Shared.NextBytes(bytebuffer);

    char[] buffer = new char[length];

    for (int i = 0; i < length; i++)
    {
        buffer[i] = characters[bytebuffer[i] % 64];
    }

    return new(buffer);
}
```

With of course an equivalent "Secure" version using `RandomNumberGenerator.Fill`.

Results:
| Method           | Categories | length | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------- |------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**     | **Secure**     | **14**     | **1,352.82 ns** |  **6.425 ns** |  **5.365 ns** |  **1.00** |    **0.01** | **0.0668** |     **560 B** |        **1.00** |
| BufferSecure     | Secure     | 14     |    85.38 ns |  0.917 ns |  0.766 ns |  0.06 |    0.00 | 0.0181 |     152 B |        0.27 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **24**     | **2,333.30 ns** | **14.006 ns** | **12.416 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| BufferSecure     | Secure     | 24     |   106.09 ns |  1.626 ns |  1.441 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **SecureRandom**     | **Secure**     | **32**     | **3,110.23 ns** | **29.132 ns** | **27.250 ns** |  **1.00** |    **0.01** | **0.2213** |    **1856 B** |        **1.00** |
| BufferSecure     | Secure     | 32     |   116.60 ns |  1.452 ns |  1.287 ns |  0.04 |    0.00 | 0.0277 |     232 B |        0.12 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **14**     |   **207.47 ns** |  **2.094 ns** |  **1.635 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| Buffer           | Vulnerable | 14     |    31.42 ns |  0.668 ns |  0.714 ns |  0.15 |    0.00 | 0.0181 |     152 B |        0.24 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **24**     |   **302.17 ns** |  **6.019 ns** |  **6.932 ns** |  **1.00** |    **0.03** | **0.1516** |    **1272 B** |        **1.00** |
| Buffer           | Vulnerable | 24     |    36.89 ns |  0.477 ns |  0.423 ns |  0.12 |    0.00 | 0.0229 |     192 B |        0.15 |
|                  |            |        |             |           |           |       |         |        |           |             |
| **GeneratePassword** | **Vulnerable** | **32**     |   **365.64 ns** |  **4.961 ns** |  **4.640 ns** |  **1.00** |    **0.02** | **0.2303** |    **1928 B** |        **1.00** |
| Buffer           | Vulnerable | 32     |    43.05 ns |  0.638 ns |  0.566 ns |  0.12 |    0.00 | 0.0277 |     232 B |        0.12 |

Now there's the improvement we were hoping for! Our Vulnerable version is now up to 8 times faster than the original co-pilot output.

More importantly, our secure version is actually faster than the original vulnerable version. 

We've sacrificed some generation strength to achieve this, can we maintain this speed while also using our full character set?  We actually might. While looking up `RandomNumberGenerator.Fill` I spotted `RandomNumberGenerator.GetItems<T>`, which:

> Creates an array populated with items chosen at random from choices

That sounds exactly like what we're after. There's also `Random.Shared.GetItems<T>`, let's implement them, going back to our original 74 character set. This leaves our methods as:

```csharp
        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GetItems(int length)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            char[] buffer = Random.Shared.GetItems<char>(characters, length);

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GetItemsSecure(int length)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            char[] buffer = RandomNumberGenerator.GetItems<char>(characters, length);

            return new(buffer);
        }
```

That's definitely a lot neater code than the original, let's see how it performs:

| Method           | Categories | length | Mean       | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|----------------- |----------- |------- |-----------:|---------:|---------:|------:|-------:|----------:|------------:|
| **SecureRandom**     | **Secure**     | **14**     | **1,356.2 ns** |  **8.22 ns** |  **7.29 ns** |  **1.00** | **0.0668** |     **560 B** |        **1.00** |
| GetItemsSecure   | Secure     | 14     |   221.3 ns |  2.16 ns |  1.91 ns |  0.16 | 0.0134 |     112 B |        0.20 |
|                  |            |        |            |          |          |       |        |           |             |
| **SecureRandom**     | **Secure**     | **24**     | **2,366.2 ns** | **14.16 ns** | **13.25 ns** |  **1.00** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsSecure   | Secure     | 24     |   302.3 ns |  4.28 ns |  4.01 ns |  0.13 | 0.0172 |     144 B |        0.12 |
|                  |            |        |            |          |          |       |        |           |             |
| **SecureRandom**     | **Secure**     | **32**     | **3,127.6 ns** | **19.27 ns** | **18.02 ns** |  **1.00** | **0.2213** |    **1856 B** |        **1.00** |
| GetItemsSecure   | Secure     | 32     |   369.0 ns |  3.69 ns |  3.45 ns |  0.12 | 0.0210 |     176 B |        0.09 |
|                  |            |        |            |          |          |       |        |           |             |
| **GeneratePassword** | **Vulnerable** | **14**     |   **208.1 ns** |  **2.20 ns** |  **1.84 ns** |  **1.00** | **0.0753** |     **632 B** |        **1.00** |
| GetItems         | Vulnerable | 14     |   130.7 ns |  0.80 ns |  0.67 ns |  0.63 | 0.0134 |     112 B |        0.18 |
|                  |            |        |            |          |          |       |        |           |             |
| **GeneratePassword** | **Vulnerable** | **24**     |   **296.6 ns** |  **3.71 ns** |  **3.10 ns** |  **1.00** | **0.1516** |    **1272 B** |        **1.00** |
| GetItems         | Vulnerable | 24     |   190.6 ns |  1.20 ns |  1.00 ns |  0.64 | 0.0172 |     144 B |        0.11 |
|                  |            |        |            |          |          |       |        |           |             |
| **GeneratePassword** | **Vulnerable** | **32**     |   **363.8 ns** |  **3.85 ns** |  **3.41 ns** |  **1.00** | **0.2303** |    **1928 B** |        **1.00** |
| GetItems         | Vulnerable | 32     |   240.8 ns |  1.46 ns |  1.14 ns |  0.66 | 0.0210 |     176 B |        0.09 |

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

In the case where we're first generating `byte[]` with our character set, we've arranged our character set so we can cheaply determine if there is a special character. We just have to look at the value of the byte pre-lookup to see if it is `55` or higher, since `55` to `63` are all special characters.

For the case where we're using `GetItems`, we have to reverse it, and check `char.IsAsciiLetterOrDigit`.

We can then compare that count against the requested minimum symbols.

```csharp
[BenchmarkCategory("Vulnerable"), Benchmark()]
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
```
With an extra parameter, we've relaxed the parameterisation of the first parameter here to avoid needing to run 9 benchmarks for each function.

For this solution we have had to enumerate the array again for the `GetItems` approach, which we expect to further worsen it's performance relative to the 64 character set approach, so let's see the results:

| Method                      | Categories | length | minmumSpecialCharacters | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------- |------- |------------------------ |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**                | **Secure**     | **24**     | **0**                       | **2,279.86 ns** | **21.121 ns** | **19.756 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 24     | 0                       |   113.81 ns |  1.473 ns |  1.378 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
| GetItemsWithRejectionSecure | Secure     | 24     | 0                       |   400.03 ns |  1.655 ns |  1.382 ns |  0.18 |    0.00 | 0.0172 |     144 B |        0.12 |
|                             |            |        |                         |             |           |           |       |         |        |           |             |
| **RejectionSampleSecure**       | **Secure**     | **24**     | **1**                       |   **113.66 ns** |  **1.752 ns** |  **1.639 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejectionSecure | Secure     | 24     | 1                       |   395.18 ns |  3.624 ns |  3.390 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                             |            |        |                         |             |           |           |       |         |        |           |             |
| **RejectionSampleSecure**       | **Secure**     | **24**     | **2**                       |   **113.70 ns** |  **0.812 ns** |  **0.678 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejectionSecure | Secure     | 24     | 2                       |   409.06 ns |  3.650 ns |  3.415 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                             |            |        |                         |             |           |           |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **24**     | **0**                       |   **312.34 ns** |  **6.177 ns** |  **5.476 ns** |  **1.00** |    **0.02** | **0.1516** |    **1272 B** |        **1.00** |
| RejectionSample             | Vulnerable | 24     | 0                       |    38.83 ns |  0.572 ns |  0.535 ns |  0.12 |    0.00 | 0.0229 |     192 B |        0.15 |
| GetItemsWithRejection       | Vulnerable | 24     | 0                       |   288.63 ns |  2.307 ns |  2.045 ns |  0.92 |    0.02 | 0.0172 |     144 B |        0.11 |
|                             |            |        |                         |             |           |           |       |         |        |           |             |
| **RejectionSample**             | **Vulnerable** | **24**     | **1**                       |    **39.04 ns** |  **0.558 ns** |  **0.522 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejection       | Vulnerable | 24     | 1                       |   291.81 ns |  1.872 ns |  1.564 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                             |            |        |                         |             |           |           |       |         |        |           |             |
| **RejectionSample**             | **Vulnerable** | **24**     | **2**                       |    **44.37 ns** |  **0.563 ns** |  **0.526 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejection       | Vulnerable | 24     | 2                       |   293.39 ns |  3.104 ns |  2.904 ns |     ? |       ? | 0.0172 |     144 B |           ? |


As expected, this has added overhead, particularly `GetItemsWithRejection`, which is neither secure, not particularly fast. If security is not an issue, then `RejectionSample` still performs decently well. If security is desired, then there is a choice between `RejectionSampleSecure` with it's slightly reduced entropy per output character, and `GetItemsWithRejectionSecure`.

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

### Moving the character map outside of the function
It's a compile time constant, so you wouldn't expect it to have any effect on performance, but just to be sure I also did a run where I moved `characters` into a field on the class. As expected the runtimes did not change.

## Addendum

Here's a benchmark run with all the functions, with a single baseline of the original co-pilot output.