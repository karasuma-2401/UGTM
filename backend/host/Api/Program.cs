using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using UGTM.BuildingBlocks.Application;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Infrastructure.Middlewares;
using UGTM.Modules.AcademicPeriods.Infrastructure;
using UGTM.Modules.DefenseCouncil.Infrastructure;
using UGTM.Modules.FinalReportEvaluation.Infrastructure;
using UGTM.Modules.GradingConsolidation.Infrastructure;
using UGTM.Modules.Identity.Infrastructure;
using UGTM.Modules.MidtermProgress.Infrastructure;
using UGTM.Modules.Notifications.Infrastructure;
using UGTM.Modules.ThesisManagement.Infrastructure;
using UGTM.Modules.TopicCatalog.Infrastructure;
using UGTM.Persistence;

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
builder.Services.AddUgtmPersistence(builder.Configuration);

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "UGTM API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập chuỗi JWT Token thu được từ API đăng nhập."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

foreach (var module in modules)
{
    module.RegisterModule(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "UGTM API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

app.Run();
