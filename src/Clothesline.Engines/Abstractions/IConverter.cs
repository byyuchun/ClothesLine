namespace Clothesline.Engines.Abstractions;

/// <summary>
/// 格式转换器核心接口
/// </summary>
public interface IConverter
{
    bool CanConvert(string sourceExt, string targetExt);

    Task<string> ConvertAsync(
        string sourceFilePath,
        string targetExt,
        string outputDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
