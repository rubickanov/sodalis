# just <recipe>   |   just --list — show all recipes

api   := "Sodalis.Host"

# list recipes (default)
default:
    @just --list

# build the solution
build:
    dotnet build

# run the API
run:
    dotnet run --project src/{{api}}

# clean build artifacts
clean:
    dotnet clean

# run tests
test:
    dotnet test

# apply .editorconfig formatting to the whole solution
format:
    dotnet format

# verify code matches .editorconfig (fails if not — for CI)
format-check:
    dotnet format --verify-no-changes

up:
    docker compose up -d

down:
    docker compose down

psql:
    docker compose exec postgres psql -U sodalis -d sodalis

# show URLs for all local services
urls:
    @echo "Sodalis services"
    @echo ""
    @echo "API"
    @echo "  base (HTTP)       http://localhost:5001"
    @echo "  base (HTTPS)      https://localhost:7219"
    @echo ""
    @echo "  Operational"
    @echo "    liveness        http://localhost:5001/health/live"
    @echo "    readiness       http://localhost:5001/health/ready"
    @echo "    version         http://localhost:5001/version"
    @echo ""
    @echo "  Docs (dev only)"
    @echo "    Scalar UI       http://localhost:5001/scalar/v1"
    @echo "    OpenAPI spec    http://localhost:5001/openapi/v1.json"
    @echo ""
    @echo "  Modules"
    @echo "    Identity API    http://localhost:5001/api/v1/auth/*"
    @echo "    Profile API     http://localhost:5001/api/v1/profile/*"
    @echo ""
    @echo "PostgreSQL"
    @echo "  host:port         localhost:5432"
    @echo "  database          sodalis"
    @echo "  user / password   sodalis / sodalis"
    @echo "  shell shortcut    just psql"
