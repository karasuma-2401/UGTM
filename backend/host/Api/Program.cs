using UGTM.BuildingBlocks.Application;
using UGTM.Modules.AcademicPeriods.Infrastructure;
using UGTM.Modules.DefenseCouncil.Infrastructure;
using UGTM.Modules.FinalReportEvaluation.Infrastructure;
using UGTM.Modules.GradingConsolidation.Infrastructure;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.MidtermProgress.Infrastructure;
using UGTM.Modules.Notifications.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules =
[
    new AcademicPeriodsModule(),
    new DefenseCouncilModule(),
    new FinalReportEvaluationModule(),
    new GradingConsolidationModule(),
    new IdentityModule(),
    new MidtermProgressModule(),
    new NotificationsModule(),
    new ThesisManagementModule(),
    new TopicCatalogModule(),
];

builder.Services.AddOpenApi();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
