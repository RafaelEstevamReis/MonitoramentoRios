#!/bin/bash

# Caminho para o diretório do projeto
PROJECT_DIR=~/monitorarios.xyz
DATA_DIR=$PROJECT_DIR/data

# Nome da imagem e do contêiner Docker
IMAGE_NAME=img_rivermon
CONTAINER_NAME=cnt_rivermon

# Vá para o diretório do projeto
cd $PROJECT_DIR

# Atualize a imagem Docker
echo "Atualizando a imagem Docker..."
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0

# Build a imagem Docker
echo "Construindo a imagem Docker..."
docker build -t $IMAGE_NAME . --no-cache

# Pare e remova o contêiner atual, se estiver rodando
echo "Parando e removendo o container atual, se estiver rodando..."
docker stop $CONTAINER_NAME || true
docker rm $CONTAINER_NAME || true

# Suba o novo contêiner com a imagem atualizada
echo "Subindo o novo container..."
docker run -d \
  --name $CONTAINER_NAME \
  -p 8091:8080 \
  -v $DATA_DIR:/app/data \
  --restart always \
  $IMAGE_NAME

echo "Novo contêiner está rodando e configurado para sempre reiniciar."

# Exibir logs do contêiner
echo "Logs do contêiner:"
docker logs -f $CONTAINER_NAME
