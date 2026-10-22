using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface ISubmissionReviewRepository
{
    Task<IReadOnlyList<SubmissionReview>> GetBySubmissionAsync(
        ReviewGateType gateType,
        Guid submissionId,
        CancellationToken cancellationToken = default);

    void Add(SubmissionReview review);
}
