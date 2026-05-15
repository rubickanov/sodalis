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
