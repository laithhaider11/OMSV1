# OMSV1
dotnet ef migrations add AttachementAdded --project OMSV1.Infrastructure --startup-project OMSV1.Application
dotnet ef database update --project OMSV1.Infrastructure --startup-project OMSV1.Application
