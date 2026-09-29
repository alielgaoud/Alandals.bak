namespace Andalos.API.Security;
public static class UploadValidation
{
    public static async Task ValidateSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var bytes = new byte[8];
        var count = await stream.ReadAsync(bytes);
        var valid = count >= 4 && (extension switch
        {
            ".jpg" or ".jpeg" => bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            ".png" => count == 8 && bytes.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".pdf" => bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F',
            _ => false
        });
        if (!valid) throw new ArgumentException("File type does not match its supported signature.");
    }
}
