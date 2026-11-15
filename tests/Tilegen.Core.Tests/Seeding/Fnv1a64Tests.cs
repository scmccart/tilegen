using System.Text;
using Tilegen.Core.Seeding;
using Xunit;

namespace Tilegen.Core.Tests.Seeding;

public class Fnv1a64Tests
{
    // The published 64-bit FNV-1a test vectors, from fnv_test_str and fnv1a_64_vector in test_fnv.c by
    // Landon Curt Noll. isthe.com/chongo/src/fnv/test_fnv.c redirects to the repository
    // https://github.com/lcn2/fnv, where the file is
    // https://raw.githubusercontent.com/lcn2/fnv/master/test_fnv.c
    // (see https://www.isthe.com/chongo/tech/comp/fnv/ for the specification).
    [Theory]
    [InlineData("", 0xcbf29ce484222325UL)]
    [InlineData("a", 0xaf63dc4c8601ec8cUL)]
    [InlineData("b", 0xaf63df4c8601f1a5UL)]
    [InlineData("fo", 0x08985907b541d342UL)]
    [InlineData("foo", 0xdcb27518fed9d577UL)]
    [InlineData("foobar", 0x85944171f73967e8UL)]
    public void The_hash_matches_the_published_test_vectors(string text, ulong expected)
    {
        Assert.Equal(expected, Fnv1a64.Hash(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void The_constants_are_the_published_offset_basis_and_prime()
    {
        Assert.Equal(0xcbf29ce484222325UL, Fnv1a64.OffsetBasis);
        Assert.Equal(0x100000001b3UL, Fnv1a64.Prime);
    }
}
