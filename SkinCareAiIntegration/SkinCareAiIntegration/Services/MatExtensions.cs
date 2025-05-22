using Emgu.CV;
using Emgu.CV.Util; // for VectorOfByte
using System.IO;

namespace SkinCareAiIntegration.Services;

public static class MatExtensions
{
    public static MemoryStream ToMemoryStream(this Mat mat)
    {
        using VectorOfByte bytes = new();
        CvInvoke.Imencode(".png", mat, bytes);
        return new MemoryStream(bytes.ToArray());
    }
}
