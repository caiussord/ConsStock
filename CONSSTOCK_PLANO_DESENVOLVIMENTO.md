# ConsStock — Plano de Desenvolvimento do Zero

> **Projeto 4 do portfólio** | API REST de gestão e controle de estoque | **Status:** planejamento | **Infraestrutura-alvo:** Microsoft Azure

## 1. Visão geral

O **ConsStock** é uma API REST para gerenciamento de estoque, construída com **C# e ASP.NET Core**. O projeto demonstrará desenvolvimento backend, modelagem de domínio, segurança, persistência, testes, Docker, CI/CD e implantação no **Microsoft Azure**.

**Objetivo de portfólio:** entregar um sistema reproduzível localmente, com código aberto e documentação profissional, e comprovar a implantação e observabilidade na Azure durante o período gratuito — sem depender de manter recursos pagos ligados permanentemente.

### Escopo do MVP

- Autenticação de usuários com JWT e autorização por papéis (`Admin` e `Operator`).
- Cadastro e consulta de **categorias** e **produtos** (SKU único, nome, descrição, preço de referência opcional, estoque mínimo e status ativo).
- Registro de **entradas**, **saídas** e **ajustes** de estoque, com motivo e usuário responsável.
- Consulta de saldo atual, histórico de movimentações e produtos abaixo do estoque mínimo.
- Paginação, filtros, ordenação e respostas de erro consistentes.
- Documentação OpenAPI, validações, logs estruturados e health checks.

**Fora do MVP:** frontend completo, compras, vendas, notas fiscais, múltiplos depósitos, integrações ERP, notificações e arquitetura de microsserviços.

## 2. Stack e decisões

| Área | Tecnologia | Motivo |
|---|---|---|
| Linguagem | C# | Ecossistema .NET |
| API | ASP.NET Core Web API (versão LTS suportada) | Backend HTTP |
| Persistência | PostgreSQL + EF Core/Npgsql | Banco relacional e migrations |
| Autenticação | ASP.NET Core Identity ou implementação segura equivalente + JWT | Gestão de credenciais e autorização |
| Testes | xUnit + testes de integração | Regressão e regras de negócio |
| Documentação | OpenAPI/Swagger UI | Contrato e exploração da API |
| Ambiente local | Docker + Docker Compose | API e PostgreSQL reproduzíveis |
| CI/CD | GitHub Actions | Build, testes e deploy |
| Infraestrutura | Azure App Service + Bicep | Deploy e infraestrutura como código |
| Observabilidade | Logs, health checks; Azure Monitor se elegível | Diagnóstico |

> **Importante:** o Azure App Service gratuito e os benefícios de PostgreSQL gerenciado possuem limites e regras próprias. Confirmar disponibilidade, região, SKU e custos estimados no Azure Portal **antes de criar** recursos. Não assumir que todos os serviços serão gratuitos durante ou após a avaliação.

## 3. Arquitetura

Adotar um **monólito modular em camadas**, sem complexidade artificial:

```text
ConsStock/
├── src/
│   ├── ConsStock.Api/             # Controllers/endpoints, DI, middleware, OpenAPI
│   ├── ConsStock.Application/     # Casos de uso, DTOs, interfaces, validações
│   ├── ConsStock.Domain/          # Entidades, regras, enums, erros de domínio
│   └── ConsStock.Infrastructure/  # EF Core, repositórios, Identity, serviços externos
├── tests/
│   ├── ConsStock.UnitTests/
│   └── ConsStock.IntegrationTests/
├── infra/
│   └── azure/
│       ├── main.bicep
│       └── README.md
├── docs/
│   ├── architecture.md
│   ├── api.md
│   ├── azure-deployment.md
│   └── screenshots/
├── .github/workflows/
│   ├── ci.yml
│   └── deploy-azure.yml
├── Dockerfile
├── compose.yaml
├── .env.example
├── .gitignore
├── README.md
└── ConsStock.sln
```

**Dependências:** Domain não depende de outras camadas; Application depende de Domain; Infrastructure implementa interfaces de Application; Api compõe as dependências.

## 4. Modelo de dados inicial

### Category
- `Id` (UUID), `Name`, `Description?`, `IsActive`, `CreatedAt`.

### Product
- `Id` (UUID), `Sku` (único), `Name`, `Description?`, `CategoryId`, `MinimumStock`, `IsActive`, `CreatedAt`, `UpdatedAt`.

### StockBalance
- `ProductId` (único), `Quantity` (decimal ou inteiro conforme unidade adotada), `Version` (controle de concorrência).

### StockMovement
- `Id` (UUID), `ProductId`, `Type` (`Entry`, `Exit`, `Adjustment`), `Quantity`, `PreviousBalance`, `NewBalance`, `Reason`, `CreatedByUserId`, `CreatedAt`, `IdempotencyKey?`.

### User
- Usuários e papéis gerenciados pelo mecanismo de identidade escolhido.

**Regras essenciais:** não permitir saldo negativo (a menos que seja uma decisão explícita futura), quantidade de entrada/saída maior que zero, SKU duplicado ou movimentação sem rastreabilidade. Saldo e histórico devem ser persistidos **na mesma transação**. Tratar concorrência entre saídas simultâneas com bloqueio/atualização condicional e testes específicos. Ajustes devem registrar saldo anterior e posterior.

## 5. Endpoints propostos

| Método | Rota | Descrição | Acesso |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Login | Público |
| GET | `/api/v1/categories` | Listar categorias | Autenticado |
| POST | `/api/v1/categories` | Criar categoria | Admin |
| GET | `/api/v1/products` | Listar produtos com filtros | Autenticado |
| GET | `/api/v1/products/{id}` | Detalhar produto | Autenticado |
| POST | `/api/v1/products` | Criar produto | Admin |
| PUT | `/api/v1/products/{id}` | Atualizar produto | Admin |
| GET | `/api/v1/stock` | Consultar saldos | Autenticado |
| POST | `/api/v1/stock/movements` | Registrar entrada/saída/ajuste | Admin/Operator |
| GET | `/api/v1/stock/movements` | Histórico paginado | Autenticado |
| GET | `/api/v1/stock/low` | Estoque abaixo do mínimo | Autenticado |
| GET | `/health` | Health check | Público, resposta mínima |

Usar `ProblemDetails` para erros, códigos HTTP adequados, validação de payload e limites de paginação. Não expor informações sensíveis nos erros.

## 6. Segurança

- Senhas com hash seguro usando componente de identidade confiável; nunca armazenar senha em texto puro.
- JWT com expiração curta, validação de emissor/audiência/assinatura e chave em secret store/variável de ambiente; nunca versionar segredos.
- Autorização por papéis e políticas em endpoints mutáveis.
- HTTPS em produção, CORS restrito, rate limiting em login e logs sem credenciais.
- Dados de demonstração fictícios; usuário de teste com permissões limitadas.
- Secrets do deploy via GitHub Environments/Secrets ou federação OIDC; preferir OIDC a credenciais persistentes quando suportado.

## 7. Plano de implementação — do zero

### Fase 1 — Fundação

1. Criar repositório público `ConsStock` e solution .NET.
2. Criar projetos Api, Application, Domain, Infrastructure e testes.
3. Configurar Docker Compose com PostgreSQL, health check e `.env.example`.
4. Configurar EF Core, migrations iniciais, tratamento global de erros e OpenAPI.
5. Criar `README.md` inicial e pipeline CI (restore, build, test).

**Critério de conclusão:** `docker compose up --build` sobe o ambiente; API responde `/health`; CI executa com sucesso.

### Fase 2 — Domínio e estoque

1. Implementar categorias e produtos com validações e SKU único.
2. Implementar saldo e movimentações transacionais.
3. Impedir saldo negativo e registrar auditoria de movimentações.
4. Adicionar paginação, filtros e consulta de estoque baixo.
5. Testar regras de negócio, incluindo concorrência de saídas simultâneas.

**Critério de conclusão:** fluxos de entrada/saída/ajuste consistentes, testados e documentados.

### Fase 3 — Segurança e qualidade

1. Implementar autenticação JWT e papéis.
2. Restringir endpoints conforme políticas.
3. Adicionar testes de integração, cenários de autorização e respostas de erro.
4. Revisar logs, validação, rate limiting e tratamento de secrets.
5. Publicar exemplos de requisições e respostas na documentação.

**Critério de conclusão:** API testável sem acesso indevido, com CI verde e instruções reproduzíveis.

### Fase 4 — Azure e infraestrutura como código

1. Conferir **data de expiração dos créditos**, elegibilidade e estimativas de custo no portal.
2. Criar Resource Group específico `rg-consstock-dev` e configurar alertas/orçamento (alertas não bloqueiam cobranças).
3. Criar Bicep para recursos selecionados; parametrizar nomes, região e SKUs.
4. Implantar a API em App Service, preferencialmente SKU gratuito quando compatível.
5. Para PostgreSQL na nuvem, usar serviço gerenciado **somente se houver crédito/benefício elegível e custo aceito**; caso contrário, documentar a limitação e validar a API localmente.
6. Configurar connection strings e segredos no Azure sem publicá-los.
7. Implementar GitHub Actions para deploy controlado (workflow manual ou ambiente protegido).
8. Validar HTTPS, health check, endpoints, logs e acesso ao banco.

**Critério de conclusão:** evidências de deploy real, pipeline executado e configuração reproduzível. Caso o banco gerenciado não seja viável, não anunciar demo completa em nuvem.

### Fase 5 — Portfólio e encerramento seguro

1. Capturar imagens da execução local, documentação OpenAPI, CI, recursos Azure e logs (ocultando IDs, segredos e dados privados).
2. Criar `docs/azure-deployment.md` com decisões, passos, limitações e custos observados.
3. Atualizar README com arquitetura, tecnologias, execução local, testes, deploy e links válidos.
4. Criar release e tag de versão.
5. **Antes do término da avaliação**, excluir recursos cobrados que não devam continuar ativos e verificar cobranças/recursos remanescentes.
6. Manter Bicep, pipelines, capturas e documentação no GitHub como evidências, mesmo sem demo pública permanente.

## 8. Testes mínimos obrigatórios

- Criar produto válido e rejeitar SKU duplicado.
- Rejeitar quantidades inválidas e saída maior que o saldo.
- Confirmar que cada movimentação altera saldo e histórico atomicamente.
- Simular duas saídas simultâneas para evitar saldo negativo.
- Testar usuário sem token, token inválido e papéis sem permissão.
- Testar filtros/paginação e erros padronizados.
- Subir ambiente limpo com Docker Compose e aplicar migrations.
- Executar testes automaticamente em pull requests.

## 9. GitHub e apresentação profissional

**Nome:** ConsStock — API REST de Gestão de Estoque

**Descrição curta sugerida:**

> API REST para controle de estoque com ASP.NET Core, PostgreSQL, Docker, autenticação JWT, testes automatizados e infraestrutura Azure como código.

**Ordem do README público:** visão geral → funcionalidades → arquitetura simplificada → stack → execução local → documentação API → testes → CI/CD → Azure (status real) → capturas → limitações → licença.

**Tópicos:** `dotnet`, `aspnetcore`, `csharp`, `postgresql`, `entity-framework-core`, `docker`, `azure`, `github-actions`, `rest-api`, `clean-architecture` (usar este último apenas se a implementação corresponder).

Não apresentar como “deploy ativo” se os recursos Azure tiverem sido removidos; preferir “implantação validada e documentada”.

## 10. Definição de pronto

- [ ] API sobe localmente via Docker Compose.
- [ ] Cadastro de produtos/categorias e movimentações funcionam.
- [ ] Regras de saldo e concorrência são testadas.
- [ ] JWT e autorização estão implementados.
- [ ] OpenAPI, health checks e erros consistentes estão disponíveis.
- [ ] CI executa build e testes.
- [ ] Bicep e instruções Azure estão versionados.
- [ ] Deploy Azure foi validado **ou** limitações foram documentadas com transparência.
- [ ] Nenhum segredo, backup real ou dado sensível foi publicado.
- [ ] README e evidências estão prontos para recrutadores.

## 11. Prompt de implementação com IA

```text
Você é um engenheiro de software sênior especialista em ASP.NET Core, PostgreSQL,
Entity Framework Core, Docker, GitHub Actions e Azure.

Vamos desenvolver do zero o projeto ConsStock, uma API REST open source de
controle de estoque para portfólio profissional. Siga o arquivo
CONSSTOCK_PLANO_DESENVOLVIMENTO.md como especificação.

Regras de trabalho:
1. Implemente somente a fase solicitada, sem antecipar funcionalidades.
2. Antes de codificar, apresente estrutura, decisões e riscos de cada fase.
3. Prefira código claro e testável, sem overengineering ou microsserviços.
4. Preserve integridade transacional de saldo e movimentações e teste concorrência.
5. Nunca inclua senhas, tokens ou connection strings reais no repositório.
6. Inclua instruções de execução, migrations e testes reproduzíveis.
7. Ao concluir cada fase, liste arquivos alterados, comandos executados,
   testes realmente rodados e pendências, sem inventar resultados.
8. Para Azure, estime custos e confirme elegibilidade antes de provisionar.

Comece pela Fase 1: estrutura da solution, projetos, Docker Compose,
PostgreSQL, health check, OpenAPI e pipeline CI.
```

---

**Diretriz principal:** construir primeiro uma API sólida e testável. Usar o Azure Free para praticar implantação real e registrar evidências, não como obrigação de manter um serviço pago funcionando permanentemente.
