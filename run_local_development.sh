docker-compose up -d \
&& dotnet ef database update \
&& cd HOAManagementCompany && dotnet run