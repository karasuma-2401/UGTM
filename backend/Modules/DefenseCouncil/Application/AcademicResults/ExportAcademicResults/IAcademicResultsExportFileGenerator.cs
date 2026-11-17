using UGTM.Modules.DefenseCouncil.Application.AcademicResults.Dtos;

namespace UGTM.Modules.DefenseCouncil.Application.AcademicResults.ExportAcademicResults;

public interface IAcademicResultsExportFileGenerator
{
    Task<AcademicResultExportFileDto> GenerateAsync(
        IReadOnlyList<AcademicResultExportRowDto> rows,
        string format,
        string phase,
        CancellationToken cancellationToken = default);
}
