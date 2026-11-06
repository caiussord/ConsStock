# ConsStock

API REST de gestão de estoque em ASP.NET Core e PostgreSQL. Este repositório está na **Fase 1 (fundação)** do [plano](CONSSTOCK_PLANO_DESENVOLVIMENTO.md). Os endpoints de categorias, produtos, movimentações e autenticação serão implementados nas próximas fases.

## Estrutura

- `src/ConsStock.Domain`: entidades e regras de domínio (Fase 2).
- `src/ConsStock.Application`: casos de uso e contratos (Fase 2).
- `src/ConsStock.Infrastructure`: EF Core, PostgreSQL e migrations.
- `src/ConsStock.Api`: composição, health checks, OpenAPI e erros.
- `tests`: testes unitários e de integração.

## Execução local com Docker

Requisitos: Docker com Compose. Copie o arquivo de exemplo e escolha uma senha apenas para este ambiente:

```powershell
Copy-Item .env.example .env
# Edite POSTGRES_PASSWORD em .env

docker compose up --build -d
```

A API fica em `http://localhost:8080`. Verifique `GET /health` (processo ativo), `GET /health/ready` (PostgreSQL acessível), `GET /openapi/v1.json` e a interface em `http://localhost:8080/swagger`.

No Compose, a API aplica as migrations ao iniciar, depois que o PostgreSQL responde ao health check. A migration `InitialFoundation` cria apenas o histórico de migrations; as tabelas de negócio entram na Fase 2.

Para parar:

```powershell
docker compose down
```

`docker compose down --volumes` apaga os dados locais do banco.

## Execução sem Docker da API

Requisitos: SDK .NET 10 e um PostgreSQL acessível. Configure `ConnectionStrings__DefaultConnection` no ambiente e aplique as migrations:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=consstock;Username=consstock;Password=SUA_SENHA_LOCAL'
dotnet ef database update --project src/ConsStock.Infrastructure --startup-project src/ConsStock.Api
dotnet run --project src/ConsStock.Api
```

O pacote de ferramentas `dotnet-ef` é necessário para o comando acima. Para criar uma migration futura: `dotnet ef migrations add Nome --project src/ConsStock.Infrastructure --startup-project src/ConsStock.Api --output-dir Persistence/Migrations`.

## Testes e CI

```powershell
dotnet restore ConsStock.sln
dotnet build ConsStock.sln --no-restore
dotnet test ConsStock.sln --no-build
```

O workflow `.github/workflows/ci.yml` executa restore, build e testes em push e pull request. O teste de integração atual valida `/health` sem depender do banco; os fluxos de estoque terão testes próprios nas fases seguintes.

## Estado e limites

Esta fase não oferece cadastro, movimentação ou login. Swagger e o documento OpenAPI estão disponíveis apenas em Development. Não há deploy Azure; os custos e a elegibilidade serão conferidos antes da Fase 4.
