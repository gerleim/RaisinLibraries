namespace Raisin.Core;

public interface IStringProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}
