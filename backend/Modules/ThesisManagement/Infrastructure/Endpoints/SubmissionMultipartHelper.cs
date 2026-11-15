using Microsoft.AspNetCore.Http;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

internal static class SubmissionMultipartHelper
{
    internal const long MaximumCombinedRequestSizeBytes =
        ThesisSubmissionFileValidator.MaximumFileSizeBytes
        + (ThesisSubmissionImageValidator.MaximumEvidenceImageCount * ThesisSubmissionImageValidator.MaximumImageFileSizeBytes)
        + 128 * 1024;

    internal static bool TryExtractProductLinks(
        IFormCollection form,
        out IReadOnlyList<SubmissionProductLinkInput>? links)
    {
        links = null;
        if (!form.TryGetValue("productLinks", out var values) || values.Count == 0)
        {
            return true;
        }

        if (values.Count != 1)
        {
            return false;
        }

        return SubmissionProductLinkValidator.TryParseJson(values[0], out links);
    }

    internal static bool TryCollectFiles(
        IFormCollection form,
        out IFormFile? mainFile,
        out IReadOnlyList<IFormFile> images,
        out bool imageTooLarge)
    {
        mainFile = null;
        images = [];
        imageTooLarge = false;

        var mainFiles = new List<IFormFile>();
        var imageFiles = new List<IFormFile>();

        foreach (var item in form.Files)
        {
            if (string.Equals(item.Name, "file", StringComparison.Ordinal))
            {
                mainFiles.Add(item);
            }
            else if (string.Equals(item.Name, "images", StringComparison.Ordinal))
            {
                imageFiles.Add(item);
            }
            else
            {
                return false;
            }
        }

        if (mainFiles.Count != 1 || mainFiles[0].Length == 0)
        {
            return false;
        }

        if (imageFiles.Count > ThesisSubmissionImageValidator.MaximumEvidenceImageCount)
        {
            return false;
        }

        foreach (var image in imageFiles)
        {
            if (image.Length == 0)
            {
                return false;
            }

            if (image.Length > ThesisSubmissionImageValidator.MaximumImageFileSizeBytes)
            {
                imageTooLarge = true;
                return false;
            }
        }

        mainFile = mainFiles[0];
        images = imageFiles;
        return true;
    }

    internal static async Task<(IReadOnlyList<SubmissionAttachmentUpload> Uploads, List<Stream> Streams)> OpenImageStreamsAsync(
        IReadOnlyList<IFormFile> imageFiles)
    {
        var uploads = new List<SubmissionAttachmentUpload>(imageFiles.Count);
        var streams = new List<Stream>(imageFiles.Count);

        try
        {
            foreach (var image in imageFiles)
            {
                var stream = image.OpenReadStream();
                streams.Add(stream);
                uploads.Add(new SubmissionAttachmentUpload(image.FileName, stream));
            }

            return (uploads, streams);
        }
        catch
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
            throw;
        }
    }
}
