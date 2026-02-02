using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;
using OpenCvSharp;

namespace NodeEditor.Plugins.OpenCv2;

public sealed class OpenCv2NodeContext : INodeContext
{
    [Node("Load Image", category: "OpenCV2/Image", description: "Load an image from disk", isCallable: false)]
    public void LoadImage(string FilePath, ImreadModes Mode, out Mat Image)
    {
        Image = Cv2.ImRead(FilePath ?? string.Empty, Mode);
    }

    [Node("Load Image Preview", category: "OpenCV2/Image", description: "Load an image and return a node preview", isCallable: false)]
    public void LoadImagePreview(string FilePath, ImreadModes Mode, out NodeImage Preview)
    {
        using var image = Cv2.ImRead(FilePath ?? string.Empty, Mode);
        Preview = CreatePreviewImage(image);
    }

    [Node("Save Image", category: "OpenCV2/Image", description: "Save an image to disk", isCallable: false)]
    public void SaveImage(Mat Image, string FilePath, out bool Success)
    {
        if (Image is null || Image.Empty())
        {
            Success = false;
            return;
        }

        Success = Cv2.ImWrite(FilePath ?? string.Empty, Image);
    }

    [Node("To Grayscale", category: "OpenCV2/Image", description: "Convert image to grayscale", isCallable: false)]
    public void ConvertToGray(Mat Image, out Mat Result)
    {
        Result = new Mat();

        if (Image is null || Image.Empty())
        {
            return;
        }

        if (Image.Channels() == 1)
        {
            Result = Image.Clone();
            return;
        }

        Cv2.CvtColor(Image, Result, ColorConversionCodes.BGR2GRAY);
    }

    [Node("Resize", category: "OpenCV2/Image", description: "Resize image to width/height", isCallable: false)]
    public void Resize(Mat Image, int Width, int Height, out Mat Result)
    {
        Result = new Mat();

        if (Image is null || Image.Empty())
        {
            return;
        }

        var width = Math.Max(1, Width);
        var height = Math.Max(1, Height);
        Cv2.Resize(Image, Result, new Size(width, height), 0, 0, InterpolationFlags.Linear);
    }

    [Node("Gaussian Blur", category: "OpenCV2/Image", description: "Apply Gaussian blur", isCallable: false)]
    public void GaussianBlur(Mat Image, int KernelSize, double Sigma, out Mat Result)
    {
        Result = new Mat();

        if (Image is null || Image.Empty())
        {
            return;
        }

        var kernel = Math.Max(1, KernelSize);
        if (kernel % 2 == 0)
        {
            kernel++;
        }

        var ksize = new Size(kernel, kernel);
        Cv2.GaussianBlur(Image, Result, ksize, Sigma, Sigma, BorderTypes.Default);
    }

    [Node("Canny", category: "OpenCV2/Image", description: "Run Canny edge detector", isCallable: false)]
    public void Canny(Mat Image, double Threshold1, double Threshold2, out Mat Result)
    {
        Result = new Mat();

        if (Image is null || Image.Empty())
        {
            return;
        }

        Cv2.Canny(Image, Result, Threshold1, Threshold2);
    }

    [Node("Threshold", category: "OpenCV2/Image", description: "Apply thresholding", isCallable: false)]
    public void Threshold(Mat Image, double Threshold, double MaxValue, ThresholdTypes Type, out Mat Result)
    {
        Result = new Mat();

        if (Image is null || Image.Empty())
        {
            return;
        }

        Cv2.Threshold(Image, Result, Threshold, MaxValue, Type);
    }

    [Node("Get Size", category: "OpenCV2/Image", description: "Get image width and height", isCallable: false)]
    public void GetImageSize(Mat Image, out int Width, out int Height)
    {
        if (Image is null || Image.Empty())
        {
            Width = 0;
            Height = 0;
            return;
        }

        Width = Image.Width;
        Height = Image.Height;
    }

    [Node("Mat To Preview", category: "OpenCV2/Image", description: "Convert a Mat to a node preview image", isCallable: false)]
    public void MatToPreview(Mat Image, out NodeImage Preview)
    {
        Preview = CreatePreviewImage(Image);
    }

    private static NodeImage CreatePreviewImage(Mat? image)
    {
        if (image is null || image.Empty())
        {
            return new NodeImage(string.Empty);
        }

        using var rgba = EnsureRgba(image);
        var width = rgba.Width;
        var height = rgba.Height;

        Cv2.ImEncode(".png", rgba, out var buffer);
        var base64 = Convert.ToBase64String(buffer);
        var dataUrl = $"data:image/png;base64,{base64}";
        return new NodeImage(dataUrl, width, height);
    }

    private static Mat EnsureRgba(Mat image)
    {
        if (image.Channels() == 4)
        {
            return image.Clone();
        }

        var converted = new Mat();
        if (image.Channels() == 1)
        {
            Cv2.CvtColor(image, converted, ColorConversionCodes.GRAY2RGBA);
        }
        else
        {
            Cv2.CvtColor(image, converted, ColorConversionCodes.BGR2RGBA);
        }

        return converted;
    }
}
