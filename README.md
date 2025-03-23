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


We can now see that avoiding `new System.Random()` increased performance, roughly 30% faster for the 24 character example.

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

The reduction in entropy for a 16 character password is going from `74^16  ~= 100 bits`, to `64^16 = 96 bits`.  So we've lost around 4 bits from our password. If this is a concern, then we can increase our password length by one character to accomodate.

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

For the case where we're using `GetItems`, we have to count `char.IsAsciiLetterOrDigit` and take that from our password length, then compare that count against the requested minimum symbols.

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

        if ((buffer.Length - buffer.Count(char.IsAsciiLetterOrDigit)) >= minmumSpecialCharacters)
        {
            return new(buffer);
        }
    }
}
```
With an extra parameter, we've relaxed the parameterisation of the first parameter here to avoid needing to run 9 benchmarks for each function.

For this solution we have had to enumerate the array again for the `GetItems` approach, which we expect to further worsen it's performance relative to the 64 character set approach, so let's see the results:

| Method                      | Categories | length | minmumSpecialCharacters | Mean        | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------- |------- |------------------------ |------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**                | **Secure**     | **24**     | **0**                       | **2,385.76 ns** | **7.992 ns** | **6.674 ns** |  **1.00** |    **0.00** | **0.1411** |    **1200 B** |        **1.00** |
| RejectionSampleSecure       | Secure     | 24     | 0                       |   112.46 ns | 1.547 ns | 1.447 ns |  0.05 |    0.00 | 0.0229 |     192 B |        0.16 |
| GetItemsWithRejectionSecure | Secure     | 24     | 0                       |   407.81 ns | 3.559 ns | 3.155 ns |  0.17 |    0.00 | 0.0172 |     144 B |        0.12 |
|                             |            |        |                         |             |          |          |       |         |        |           |             |
| **RejectionSampleSecure**       | **Secure**     | **24**     | **1**                       |   **113.10 ns** | **1.644 ns** | **1.538 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejectionSecure | Secure     | 24     | 1                       |   404.39 ns | 3.120 ns | 2.918 ns |     ? |       ? | 0.0172 |     145 B |           ? |
|                             |            |        |                         |             |          |          |       |         |        |           |             |
| **RejectionSampleSecure**       | **Secure**     | **24**     | **2**                       |   **115.07 ns** | **0.368 ns** | **0.287 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejectionSecure | Secure     | 24     | 2                       |   442.09 ns | 2.638 ns | 2.338 ns |     ? |       ? | 0.0176 |     150 B |           ? |
|                             |            |        |                         |             |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **24**     | **0**                       |   **300.58 ns** | **3.271 ns** | **2.732 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| RejectionSample             | Vulnerable | 24     | 0                       |    54.66 ns | 0.760 ns | 0.711 ns |  0.18 |    0.00 | 0.0229 |     192 B |        0.15 |
| GetItemsWithRejection       | Vulnerable | 24     | 0                       |   290.15 ns | 2.151 ns | 1.906 ns |  0.97 |    0.01 | 0.0172 |     144 B |        0.11 |
|                             |            |        |                         |             |          |          |       |         |        |           |             |
| **RejectionSample**             | **Vulnerable** | **24**     | **1**                       |    **38.47 ns** | **0.190 ns** | **0.148 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejection       | Vulnerable | 24     | 1                       |   289.28 ns | 0.636 ns | 0.497 ns |     ? |       ? | 0.0172 |     145 B |           ? |
|                             |            |        |                         |             |          |          |       |         |        |           |             |
| **RejectionSample**             | **Vulnerable** | **24**     | **2**                       |    **44.44 ns** | **0.422 ns** | **0.394 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| GetItemsWithRejection       | Vulnerable | 24     | 2                       |   320.36 ns | 2.607 ns | 2.439 ns |     ? |       ? | 0.0176 |     150 B |           ? |


As expected, this has added overhead, particularly `GetItemsWithRejection`, which is left in a strange spot of being neither secure, nor particularly fast. If security is not an issue, then `RejectionSample` still performs decently well. If security is desired, then there is a choice between `RejectionSampleSecure` with it's slightly reduced entropy per output character, and `GetItemsWithRejectionSecure`.

We can try to speed up the `GetItems` based methods by using a loop to count the special characters, so we can exit early when we've met our target rather than counting all special characters.

Finally, we can try to avoid a heap allocation by using `stackalloc` to allocate the span on the stack.

| Method                      | Categories | length | minmumSpecialCharacters | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------- |------- |------------------------ |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| **SecureRandom**                | **Secure**     | **24**     | **0**                       | **2,376.9 ns** | **20.84 ns** | **19.49 ns** |  **1.00** |    **0.01** | **0.1411** |    **1200 B** |        **1.00** |
| GetItemsWithRejectionSecure | Secure     | 24     | 0                       |   397.5 ns |  2.25 ns |  1.88 ns |  0.17 |    0.00 | 0.0172 |     144 B |        0.12 |
| SpecialLoopSecure           | Secure     | 24     | 0                       |   320.2 ns |  1.59 ns |  1.33 ns |  0.13 |    0.00 | 0.0172 |     144 B |        0.12 |
| StackAllocSecure            | Secure     | 24     | 0                       |   321.1 ns |  1.52 ns |  1.27 ns |  0.14 |    0.00 | 0.0086 |      72 B |        0.06 |
|                             |            |        |                         |            |          |          |       |         |        |           |             |
| **GetItemsWithRejectionSecure** | **Secure**     | **24**     | **1**                       |   **409.0 ns** |  **2.60 ns** |  **2.44 ns** |     **?** |       **?** | **0.0172** |     **145 B** |           **?** |
| SpecialLoopSecure           | Secure     | 24     | 1                       |   321.4 ns |  2.31 ns |  2.16 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAllocSecure            | Secure     | 24     | 1                       |   321.4 ns |  1.97 ns |  1.74 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                             |            |        |                         |            |          |          |       |         |        |           |             |
| **GetItemsWithRejectionSecure** | **Secure**     | **24**     | **2**                       |   **436.2 ns** |  **5.01 ns** |  **4.68 ns** |     **?** |       **?** | **0.0176** |     **150 B** |           **?** |
| SpecialLoopSecure           | Secure     | 24     | 2                       |   360.9 ns |  3.98 ns |  3.72 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAllocSecure            | Secure     | 24     | 2                       |   357.4 ns |  2.41 ns |  2.25 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                             |            |        |                         |            |          |          |       |         |        |           |             |
| **GeneratePassword**            | **Vulnerable** | **24**     | **0**                       |   **298.9 ns** |  **2.96 ns** |  **2.31 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| GetItemsWithRejection       | Vulnerable | 24     | 0                       |   289.4 ns |  1.56 ns |  1.30 ns |  0.97 |    0.01 | 0.0172 |     144 B |        0.11 |
| SpecialLoop                 | Vulnerable | 24     | 0                       |   202.6 ns |  0.86 ns |  0.67 ns |  0.68 |    0.01 | 0.0172 |     144 B |        0.11 |
| StackAlloc                  | Vulnerable | 24     | 0                       |   201.7 ns |  1.78 ns |  1.67 ns |  0.67 |    0.01 | 0.0086 |      72 B |        0.06 |
|                             |            |        |                         |            |          |          |       |         |        |           |             |
| **GetItemsWithRejection**       | **Vulnerable** | **24**     | **1**                       |   **294.3 ns** |  **2.12 ns** |  **1.88 ns** |     **?** |       **?** | **0.0172** |     **145 B** |           **?** |
| SpecialLoop                 | Vulnerable | 24     | 1                       |   204.8 ns |  2.75 ns |  2.57 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAlloc                  | Vulnerable | 24     | 1                       |   201.0 ns |  1.07 ns |  0.95 ns |     ? |       ? | 0.0086 |      72 B |           ? |
|                             |            |        |                         |            |          |          |       |         |        |           |             |
| **GetItemsWithRejection**       | **Vulnerable** | **24**     | **2**                       |   **312.3 ns** |  **1.09 ns** |  **0.91 ns** |     **?** |       **?** | **0.0176** |     **150 B** |           **?** |
| SpecialLoop                 | Vulnerable | 24     | 2                       |   232.5 ns |  1.14 ns |  0.95 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| StackAlloc                  | Vulnerable | 24     | 2                       |   230.0 ns |  2.54 ns |  2.37 ns |     ? |       ? | 0.0086 |      72 B |           ? |

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

### Moving the character map outside of the function
It's a compile time constant, so you wouldn't expect it to have any effect on performance, but just to be sure I also did a run where I moved `characters` into a field on the class. As expected the runtimes did not change.

## Addendum

Here's a benchmark run with all the functions, with a single baseline of the original co-pilot output. The arguments were changed to properties to support cleaner parameterisation within BenchmarkDotNet and a shared common baseline set.

<details>

<summary>Full results comparison</summary>


| Method                       | Categories | length | minmumSpecialCharacters | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------- |----------- |------- |------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| **GeneratePassword**             | ****           | **14**     | **0**                       |   **219.15 ns** |  **3.777 ns** |  **3.533 ns** |   **218.45 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 0                       | 1,399.80 ns | 10.911 ns | 10.206 ns | 1,402.17 ns |  6.39 |    0.11 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 0                       |   132.45 ns |  2.005 ns |  1.674 ns |   132.13 ns |  0.60 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **14**     | **1**                       |   **220.16 ns** |  **2.504 ns** |  **2.220 ns** |   **219.45 ns** |  **1.00** |    **0.01** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 1                       | 1,345.98 ns | 10.294 ns |  9.629 ns | 1,344.02 ns |  6.11 |    0.07 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 1                       |   130.18 ns |  1.264 ns |  1.055 ns |   130.34 ns |  0.59 |    0.01 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **14**     | **2**                       |   **215.84 ns** |  **4.039 ns** |  **3.778 ns** |   **215.82 ns** |  **1.00** |    **0.02** | **0.0753** |     **632 B** |        **1.00** |
| SecureRandom                 |            | 14     | 2                       | 1,364.52 ns |  9.562 ns |  8.477 ns | 1,361.26 ns |  6.32 |    0.11 | 0.0668 |     560 B |        0.89 |
| GeneratePasswordSharedRandom |            | 14     | 2                       |   131.08 ns |  2.651 ns |  3.886 ns |   131.31 ns |  0.61 |    0.02 | 0.0668 |     560 B |        0.89 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **0**                       |   **308.24 ns** |  **3.973 ns** |  **3.318 ns** |   **309.45 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 0                       | 2,219.32 ns | 24.466 ns | 22.885 ns | 2,213.43 ns |  7.20 |    0.10 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 0                       |   231.78 ns |  4.429 ns |  3.926 ns |   229.76 ns |  0.75 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **1**                       |   **311.60 ns** |  **6.065 ns** |  **5.957 ns** |   **309.58 ns** |  **1.00** |    **0.03** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 1                       | 2,329.18 ns | 22.799 ns | 21.326 ns | 2,332.30 ns |  7.48 |    0.15 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 1                       |   233.33 ns |  4.394 ns |  4.513 ns |   233.14 ns |  0.75 |    0.02 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **24**     | **2**                       |   **307.51 ns** |  **3.086 ns** |  **2.577 ns** |   **306.11 ns** |  **1.00** |    **0.01** | **0.1516** |    **1272 B** |        **1.00** |
| SecureRandom                 |            | 24     | 2                       | 2,366.38 ns | 19.228 ns | 17.986 ns | 2,361.68 ns |  7.70 |    0.08 | 0.1411 |    1200 B |        0.94 |
| GeneratePasswordSharedRandom |            | 24     | 2                       |   236.00 ns |  4.727 ns |  4.191 ns |   234.93 ns |  0.77 |    0.01 | 0.1433 |    1200 B |        0.94 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **0**                       |   **388.60 ns** |  **7.489 ns** |  **8.624 ns** |   **386.26 ns** |  **1.00** |    **0.03** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 0                       | 3,216.55 ns | 23.617 ns | 19.721 ns | 3,216.24 ns |  8.28 |    0.19 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 0                       |   305.47 ns |  0.898 ns |  0.750 ns |   305.38 ns |  0.79 |    0.02 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **1**                       |   **386.91 ns** |  **7.637 ns** |  **8.172 ns** |   **384.83 ns** |  **1.00** |    **0.03** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 1                       | 3,148.02 ns | 28.376 ns | 26.543 ns | 3,139.43 ns |  8.14 |    0.18 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 1                       |   307.88 ns |  3.509 ns |  3.283 ns |   306.87 ns |  0.80 |    0.02 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **GeneratePassword**             | ****           | **32**     | **2**                       |   **386.19 ns** |  **3.782 ns** |  **3.158 ns** |   **386.55 ns** |  **1.00** |    **0.01** | **0.2303** |    **1928 B** |        **1.00** |
| SecureRandom                 |            | 32     | 2                       | 3,146.05 ns | 24.294 ns | 22.724 ns | 3,135.68 ns |  8.15 |    0.09 | 0.2213 |    1856 B |        0.96 |
| GeneratePasswordSharedRandom |            | 32     | 2                       |   316.71 ns |  2.922 ns |  2.281 ns |   316.13 ns |  0.82 |    0.01 | 0.2217 |    1856 B |        0.96 |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **0**                       | **1,182.97 ns** |  **8.203 ns** |  **7.673 ns** | **1,184.59 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 0                       | 1,173.34 ns |  5.656 ns |  5.014 ns | 1,173.69 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 0                       |    87.29 ns |  0.859 ns |  0.761 ns |    87.04 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 0                       |   213.18 ns |  1.963 ns |  1.740 ns |   212.62 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 0                       |    97.78 ns |  0.698 ns |  0.619 ns |    97.68 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 0                       |   277.61 ns |  1.198 ns |  1.062 ns |   277.86 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **1**                       | **1,193.72 ns** |  **5.738 ns** |  **5.367 ns** | **1,192.97 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 1                       | 1,166.09 ns |  5.210 ns |  4.874 ns | 1,166.42 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 1                       |    85.78 ns |  0.408 ns |  0.341 ns |    85.75 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 1                       |   210.18 ns |  1.644 ns |  1.538 ns |   210.12 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 1                       |   102.51 ns |  1.233 ns |  1.154 ns |   101.94 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 1                       |   273.69 ns |  1.700 ns |  1.507 ns |   273.27 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **14**     | **2**                       | **1,182.15 ns** |  **5.049 ns** |  **4.723 ns** | **1,181.70 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArraySecure              | Secure     | 14     | 2                       | 1,168.72 ns |  5.061 ns |  4.734 ns | 1,167.37 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| BufferSecure                 | Secure     | 14     | 2                       |    85.99 ns |  0.779 ns |  0.651 ns |    85.88 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsSecure               | Secure     | 14     | 2                       |   207.86 ns |  1.286 ns |  1.140 ns |   207.60 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSampleSecure        | Secure     | 14     | 2                       |   107.01 ns |  0.864 ns |  0.808 ns |   106.91 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 14     | 2                       |   273.00 ns |  1.704 ns |  1.511 ns |   272.74 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **0**                       | **2,004.73 ns** |  **7.906 ns** |  **7.395 ns** | **2,005.34 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 0                       | 2,095.69 ns | 10.514 ns |  9.834 ns | 2,095.61 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 0                       |   109.34 ns |  0.995 ns |  0.831 ns |   109.36 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 0                       |   307.73 ns |  2.672 ns |  2.499 ns |   308.21 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 0                       |   135.94 ns |  1.399 ns |  1.168 ns |   136.09 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 0                       |   407.54 ns |  3.706 ns |  3.467 ns |   405.70 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **1**                       | **2,014.45 ns** | **14.037 ns** | **13.131 ns** | **2,011.74 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 1                       | 2,162.48 ns | 10.304 ns |  9.134 ns | 2,162.99 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 1                       |   107.20 ns |  0.504 ns |  0.421 ns |   107.16 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 1                       |   305.07 ns |  2.691 ns |  2.386 ns |   304.58 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 1                       |   126.57 ns |  0.917 ns |  0.766 ns |   126.66 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 1                       |   398.22 ns |  3.846 ns |  3.597 ns |   398.24 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **24**     | **2**                       | **1,998.26 ns** |  **5.488 ns** |  **4.865 ns** | **1,998.87 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArraySecure              | Secure     | 24     | 2                       | 1,984.91 ns |  8.071 ns |  7.550 ns | 1,985.44 ns |     ? |       ? | 0.0153 |     144 B |           ? |
| BufferSecure                 | Secure     | 24     | 2                       |   107.91 ns |  1.708 ns |  1.598 ns |   108.41 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsSecure               | Secure     | 24     | 2                       |   297.10 ns |  3.325 ns |  3.110 ns |   296.69 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSampleSecure        | Secure     | 24     | 2                       |   129.25 ns |  1.221 ns |  1.020 ns |   129.25 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 24     | 2                       |   400.07 ns |  2.297 ns |  2.036 ns |   400.41 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **0**                       | **2,655.31 ns** | **11.702 ns** | **10.946 ns** | **2,655.03 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 0                       | 2,636.62 ns | 12.680 ns | 11.861 ns | 2,636.14 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 0                       |   119.76 ns |  0.774 ns |  0.647 ns |   119.64 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 0                       |   365.71 ns |  3.268 ns |  2.729 ns |   366.15 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 0                       |   155.40 ns |  3.041 ns |  4.060 ns |   155.89 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 0                       |   508.58 ns |  6.724 ns |  6.289 ns |   506.93 ns |     ? |       ? | 0.0210 |     176 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **1**                       | **2,646.65 ns** |  **8.741 ns** |  **8.176 ns** | **2,645.78 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 1                       | 2,638.75 ns | 10.741 ns | 10.047 ns | 2,638.00 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 1                       |   120.48 ns |  1.569 ns |  1.391 ns |   120.27 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 1                       |   372.55 ns |  1.436 ns |  1.200 ns |   372.14 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 1                       |   150.59 ns |  3.050 ns |  5.422 ns |   152.15 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 1                       |   519.25 ns |  4.025 ns |  3.568 ns |   518.56 ns |     ? |       ? | 0.0210 |     176 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilderSecure**          | **Secure**     | **32**     | **2**                       | **2,662.23 ns** |  **9.570 ns** |  **8.484 ns** | **2,662.85 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArraySecure              | Secure     | 32     | 2                       | 2,628.69 ns | 14.677 ns | 13.729 ns | 2,628.45 ns |     ? |       ? | 0.0191 |     176 B |           ? |
| BufferSecure                 | Secure     | 32     | 2                       |   118.75 ns |  1.378 ns |  1.222 ns |   118.28 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsSecure               | Secure     | 32     | 2                       |   375.85 ns |  2.829 ns |  2.647 ns |   375.38 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSampleSecure        | Secure     | 32     | 2                       |   158.73 ns |  3.131 ns |  2.929 ns |   159.97 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejectionSecure  | Secure     | 32     | 2                       |   502.76 ns |  4.484 ns |  3.975 ns |   501.64 ns |     ? |       ? | 0.0210 |     176 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **0**                       |   **105.88 ns** |  **2.054 ns** |  **2.110 ns** |   **106.44 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 0                       |    66.86 ns |  0.255 ns |  0.199 ns |    66.81 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 0                       |    33.90 ns |  0.537 ns |  0.476 ns |    33.92 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 0                       |   125.21 ns |  2.036 ns |  1.700 ns |   124.41 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 0                       |    37.23 ns |  0.752 ns |  0.667 ns |    37.16 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 0                       |   185.37 ns |  0.754 ns |  0.629 ns |   185.14 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **1**                       |   **115.55 ns** |  **2.366 ns** |  **4.143 ns** |   **115.95 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 1                       |    67.54 ns |  0.454 ns |  0.379 ns |    67.37 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 1                       |    33.54 ns |  0.375 ns |  0.292 ns |    33.60 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 1                       |   124.79 ns |  1.638 ns |  1.452 ns |   124.10 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 1                       |    37.10 ns |  0.680 ns |  0.568 ns |    37.13 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 1                       |   191.19 ns |  1.322 ns |  1.104 ns |   190.94 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **14**     | **2**                       |   **107.24 ns** |  **4.792 ns** | **14.130 ns** |   **114.37 ns** |     **?** |       **?** | **0.0191** |     **160 B** |           **?** |
| CharArray                    | Vulnerable | 14     | 2                       |    67.46 ns |  0.412 ns |  0.321 ns |    67.46 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| Buffer                       | Vulnerable | 14     | 2                       |    39.53 ns |  0.729 ns |  0.646 ns |    39.45 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItems                     | Vulnerable | 14     | 2                       |   126.21 ns |  0.832 ns |  0.695 ns |   125.99 ns |     ? |       ? | 0.0134 |     112 B |           ? |
| RejectionSample              | Vulnerable | 14     | 2                       |    48.38 ns |  0.738 ns |  0.616 ns |    48.41 ns |     ? |       ? | 0.0181 |     152 B |           ? |
| GetItemsWithRejection        | Vulnerable | 14     | 2                       |   191.95 ns |  1.852 ns |  1.733 ns |   191.02 ns |     ? |       ? | 0.0134 |     112 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **0**                       |   **154.97 ns** |  **5.280 ns** | **15.568 ns** |   **158.79 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 0                       |   109.65 ns |  1.099 ns |  1.028 ns |   109.27 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 0                       |    38.39 ns |  0.497 ns |  0.441 ns |    38.25 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 0                       |   190.83 ns |  1.065 ns |  0.889 ns |   190.96 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 0                       |    44.12 ns |  0.266 ns |  0.222 ns |    44.11 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 0                       |   288.47 ns |  1.063 ns |  0.887 ns |   288.22 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **1**                       |   **165.04 ns** |  **8.625 ns** | **25.430 ns** |   **167.43 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 1                       |   109.75 ns |  1.079 ns |  1.010 ns |   109.58 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 1                       |    37.65 ns |  0.562 ns |  0.526 ns |    37.45 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 1                       |   186.87 ns |  0.617 ns |  0.515 ns |   186.79 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 1                       |    44.95 ns |  0.734 ns |  0.687 ns |    44.55 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 1                       |   295.83 ns |  0.716 ns |  0.598 ns |   295.83 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **24**     | **2**                       |   **186.45 ns** |  **4.239 ns** | **12.498 ns** |   **192.00 ns** |     **?** |       **?** | **0.0229** |     **192 B** |           **?** |
| CharArray                    | Vulnerable | 24     | 2                       |   109.88 ns |  1.283 ns |  1.200 ns |   109.57 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| Buffer                       | Vulnerable | 24     | 2                       |    37.36 ns |  0.416 ns |  0.389 ns |    37.29 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItems                     | Vulnerable | 24     | 2                       |   186.88 ns |  1.160 ns |  0.969 ns |   186.67 ns |     ? |       ? | 0.0172 |     144 B |           ? |
| RejectionSample              | Vulnerable | 24     | 2                       |    55.66 ns |  0.270 ns |  0.226 ns |    55.55 ns |     ? |       ? | 0.0229 |     192 B |           ? |
| GetItemsWithRejection        | Vulnerable | 24     | 2                       |   298.51 ns |  3.398 ns |  3.178 ns |   297.98 ns |     ? |       ? | 0.0172 |     144 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **0**                       |   **202.36 ns** |  **4.108 ns** |  **9.842 ns** |   **199.72 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 0                       |   145.02 ns |  1.463 ns |  1.297 ns |   145.01 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 0                       |    45.50 ns |  0.296 ns |  0.247 ns |    45.47 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 0                       |   244.56 ns |  0.914 ns |  0.714 ns |   244.59 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 0                       |    53.27 ns |  0.671 ns |  0.628 ns |    53.02 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 0                       |   376.33 ns |  3.191 ns |  2.985 ns |   375.26 ns |     ? |       ? | 0.0210 |     176 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **1**                       |   **245.84 ns** |  **4.515 ns** |  **5.018 ns** |   **244.83 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 1                       |   143.42 ns |  0.521 ns |  0.435 ns |   143.37 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 1                       |    44.52 ns |  0.568 ns |  0.531 ns |    44.40 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 1                       |   239.76 ns |  1.815 ns |  1.609 ns |   239.65 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 1                       |    53.29 ns |  0.788 ns |  0.737 ns |    53.00 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 1                       |   376.41 ns |  2.455 ns |  2.297 ns |   376.25 ns |     ? |       ? | 0.0210 |     176 B |           ? |
|                              |            |        |                         |             |           |           |             |       |         |        |           |             |
| **StringBuilder**                | **Vulnerable** | **32**     | **2**                       |   **249.48 ns** |  **5.002 ns** | **11.083 ns** |   **252.35 ns** |     **?** |       **?** | **0.0267** |     **224 B** |           **?** |
| CharArray                    | Vulnerable | 32     | 2                       |   141.39 ns |  1.406 ns |  1.174 ns |   140.95 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| Buffer                       | Vulnerable | 32     | 2                       |    44.48 ns |  0.619 ns |  0.579 ns |    44.22 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItems                     | Vulnerable | 32     | 2                       |   242.01 ns |  1.820 ns |  1.520 ns |   242.25 ns |     ? |       ? | 0.0210 |     176 B |           ? |
| RejectionSample              | Vulnerable | 32     | 2                       |    65.83 ns |  1.032 ns |  0.862 ns |    65.81 ns |     ? |       ? | 0.0277 |     232 B |           ? |
| GetItemsWithRejection        | Vulnerable | 32     | 2                       |   363.38 ns |  1.851 ns |  1.546 ns |   363.12 ns |     ? |       ? | 0.0210 |     176 B |           ? |

</details>