using System.Globalization;
using System.Text;
namespace AccessGuard.Security;
public static class SearchText {
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
}
