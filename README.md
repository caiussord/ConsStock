# ConsStock

API REST de gestão de estoque em ASP.NET Core e PostgreSQL. As **Fases 1 a 3** do [plano](CONSSTOCK_PLANO_DESENVOLVIMENTO.md) estão implementadas. A implantação Azure pertence à Fase 4.

## Estrutura

- `src/ConsStock.Domain`: entidades e regras de saldo.
- `src/ConsStock.Application`: contrato do serviço de movimentação.
- `src/ConsStock.Infrastructure`: EF Core, PostgreSQL e migrations.
- `src/ConsStock.Api`: composição, health checks, OpenAPI e erros.
- `tests`: testes unitários e de integração.

## Execução local com Docker

Requisitos: Docker com Compose. Copie o arquivo de exemplo e preencha `POSTGRES_PASSWORD`, `JWT_KEY`, `BOOTSTRAP_ADMIN_EMAIL` e `BOOTSTRAP_ADMIN_PASSWORD` com valores locais. A chave JWT deve ter pelo menos 32 bytes; a senha do administrador deve ter ao menos 12 caracteres, incluindo maiúscula, minúscula, dígito e símbolo. Você pode gerar valores aleatórios no PowerShell:

```powershell
Copy-Item .env.example .env
$jwtKey = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$adminPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) + 'aA1!'
# Copie os valores de $jwtKey e $adminPassword para .env e edite POSTGRES_PASSWORD e BOOTSTRAP_ADMIN_EMAIL.

docker compose up --build -d
```

A API fica em `http://localhost:8080`. Verifique `GET /health` (processo ativo), `GET /health/ready` (PostgreSQL acessível), `GET /openapi/v1.json` e a interface em `http://localhost:8080/swagger`.

No Compose, a API aplica as migrations ao iniciar, depois que o PostgreSQL responde ao health check. `InitialFoundation` cria o histórico de migrations, `InventorySchema` cria as tabelas de negócio e `IdentityAndRoles` cria as tabelas de usuários e papéis. O administrador inicial é criado uma vez; mudar a senha no `.env` depois disso não altera a senha no banco existente.

Para parar:

```powershell
docker compose down
```

`docker compose down --volumes` apaga os dados locais do banco.

## Execução sem Docker da API

Requisitos: SDK .NET 10 e um PostgreSQL acessível. Configure `ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key` e, para um banco novo, `Bootstrap__AdminEmail` e `Bootstrap__AdminPassword` no ambiente. Aplique as migrations:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=consstock;Username=consstock;Password=SUA_SENHA_LOCAL'
$env:Jwt__Issuer = 'ConsStock'
$env:Jwt__Audience = 'ConsStock.Client'
$env:Jwt__Key = 'SUA_CHAVE_ALEATORIA_DE_PELO_MENOS_32_BYTES'
$env:Bootstrap__AdminEmail = 'SEU_EMAIL_LOCAL'
$env:Bootstrap__AdminPassword = 'SUA_SENHA_FORTE_LOCAL'
$env:Database__ApplyMigrations = 'true'
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

O workflow `.github/workflows/ci.yml` inicia um PostgreSQL descartável e executa restore, build e testes em push e pull request. Os testes cobrem regras de saldo, fluxo HTTP, SKU duplicado, concorrência entre saídas, rollback, autenticação, papéis e limite de tentativas de login.

## API

Veja [rotas, payloads e exemplos](docs/api.md). Faça login em `/api/v1/auth/login` e envie `Authorization: Bearer <token>` nas rotas de negócio. O administrador pode criar usuários Operator em `/api/v1/auth/users`.

## Estado e limites

Swagger e o documento OpenAPI estão disponíveis apenas em Development. O estoque é contado em unidades inteiras; no ajuste, `quantity` representa o saldo final desejado (zero é permitido). Saídas simultâneas usam atualização condicional por versão e a API responde `409` quando o saldo mudou ou é insuficiente. O JWT expira após 15 minutos; não há refresh token nesta fase. Não há deploy Azure; os custos e a elegibilidade serão conferidos antes da Fase 4.
