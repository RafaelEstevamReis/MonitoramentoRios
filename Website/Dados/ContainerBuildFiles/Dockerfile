# Use a imagem base oficial do .NET SDK para buildar a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Baixe o repositório
RUN apt-get update && apt-get install -y git
RUN git clone https://github.com/RafaelEstevamReis/MonitoramentoRios.git
RUN git -C /app/MonitoramentoRios rev-parse HEAD > /app/gitHash.txt

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
COPY --from=build /app/gitHash.txt .
COPY appsettings.json .

RUN HASH=$(head -c 7 /app/gitHash.txt) && sed -i "s|{{GIT_HASH}}|$HASH|g" /app/wwwroot/index.html

# Exponha a porta na qual a aplicação vai rodar
EXPOSE 8080

# Rode a aplicação
ENTRYPOINT ["dotnet", "Web.Data.dll"]
