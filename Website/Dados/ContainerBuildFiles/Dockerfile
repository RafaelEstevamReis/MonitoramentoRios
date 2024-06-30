# Use a imagem base oficial do .NET SDK para buildar a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Baixe o repositório
RUN apt-get update && apt-get install -y git
RUN git clone https://github.com/RafaelEstevamReis/MonitoramentoRios.git
WORKDIR /app/MonitoramentoRios/Website/Dados

# Restaure as dependências
RUN dotnet restore

# Compile a aplicação
RUN dotnet publish -c Release -o /app/publish

# Use a imagem base oficial do .NET Runtime para rodar a aplicação
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copie os arquivos compilados da imagem de build
COPY --from=build /app/publish .

# Exponha a porta na qual a aplicação vai rodar
EXPOSE 8080

#RUN chown -R 1000:1000 ~/app/wwwroot
# Rode a aplicação
ENTRYPOINT ["dotnet", "Web.Data.dll"]
