docker compose up -d db \
&& dotnet ef database update \
&& cd HOAManagementCompany && dotnet run