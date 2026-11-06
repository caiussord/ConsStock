# ConsStock

API REST de gestão de estoque em ASP.NET Core e PostgreSQL. As **Fases 1 e 2** do [plano](CONSSTOCK_PLANO_DESENVOLVIMENTO.md) estão implementadas. A autenticação e os papéis pertencem à Fase 3.

## Estrutura

- `src/ConsStock.Domain`: entidades e regras de saldo.
- `src/ConsStock.Application`: contrato do serviço de movimentação.
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

No Compose, a API aplica as migrations ao iniciar, depois que o PostgreSQL responde ao health check. `InitialFoundation` cria o histórico de migrations e `InventorySchema` cria as tabelas de negócio.

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

Para executar os testes de integração localmente, mantenha o Compose ativo e configure `TEST_DATABASE_CONNECTION` com a mesma senha escolhida no `.env`:

```powershell
$env:TEST_DATABASE_CONNECTION = 'Host=localhost;Port=5432;Database=consstock;Username=consstock;Password=SUA_SENHA_LOCAL'
dotnet test ConsStock.sln --configuration Release
```

O workflow `.github/workflows/ci.yml` inicia um PostgreSQL descartável e executa restore, build e testes em push e pull request. Os testes cobrem regras de saldo, fluxo HTTP, SKU duplicado, concorrência entre saídas e rollback quando a gravação do histórico falha.

## API

Veja [rotas, payloads e exemplos](docs/api.md). Nesta fase os endpoints de negócio ainda são públicos; a autenticação e a autorização serão adicionadas na Fase 3. O campo `createdByUserId` é informado pelo cliente para os testes desta fase e ainda não representa identidade verificada.

## Estado e limites

Esta fase não oferece login. Swagger e o documento OpenAPI estão disponíveis apenas em Development. O estoque é contado em unidades inteiras; no ajuste, `quantity` representa o saldo final desejado (zero é permitido). Saídas simultâneas usam atualização condicional por versão e a API responde `409` quando o saldo mudou ou é insuficiente. Não há deploy Azure; os custos e a elegibilidade serão conferidos antes da Fase 4.
