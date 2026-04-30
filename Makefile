SOLUTION       := NGql.sln
CORE_PROJECT   := src/Core/Core.csproj
CONFIG         := Release
ARTIFACTS      := $(CURDIR)/artifacts
COVERLET_DIR   := $(ARTIFACTS)/test-results/coverlet
JUNIT_DIR      := $(ARTIFACTS)/test-results/junit
COVERAGE_DIR   := $(ARTIFACTS)/coverage-report
PACKAGES_DIR   := $(ARTIFACTS)/packages
NUGET_SOURCE   := https://api.nuget.org/v3/index.json
NUGET_API_KEY  ?=

.DEFAULT_GOAL := test
.PHONY: help clean restore build test coverage report pack publish ci tools

help:
	@echo "Targets:"
	@echo "  clean      Remove artifacts/ and bin/obj output"
	@echo "  restore    dotnet restore $(SOLUTION)"
	@echo "  build      dotnet build $(SOLUTION) -c $(CONFIG)"
	@echo "  test       Run tests with coverage (cobertura + junit)"
	@echo "  report     Generate HTML coverage report + badges"
	@echo "  coverage   test + report"
	@echo "  pack       dotnet pack $(CORE_PROJECT) -> $(PACKAGES_DIR)"
	@echo "  publish    Push *.nupkg to NuGet (needs NUGET_API_KEY)"
	@echo "  ci         restore + build + coverage (used by GitHub Actions)"
	@echo "  tools      Install dotnet-reportgenerator-globaltool"

clean:
	rm -rf $(ARTIFACTS)
	find . -type d \( -name bin -o -name obj \) -not -path "./.git/*" -prune -exec rm -rf {} +

restore:
	dotnet restore $(SOLUTION)

build: restore
	dotnet build $(SOLUTION) --configuration $(CONFIG) --no-restore

test: build
	dotnet test $(SOLUTION) \
		--configuration $(CONFIG) \
		--no-build \
		--results-directory $(ARTIFACTS)/test-results \
		--logger "junit;LogFilePath=$(JUNIT_DIR)/{assembly}.xml;MethodFormat=Class;FailureBodyFormat=Verbose" \
		/p:CollectCoverage=true \
		/p:CoverletOutputFormat=cobertura \
		/p:CoverletOutput=$(COVERLET_DIR)/ \
		/p:ExcludeByFile=\"*.Generated.cs\"

tools:
	@command -v reportgenerator >/dev/null 2>&1 || dotnet tool install -g dotnet-reportgenerator-globaltool

report: tools
	reportgenerator \
		"-reports:$(COVERLET_DIR)/*.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)" \
		"-reporttypes:HtmlInline_AzurePipelines;Badges"

coverage: test report

pack: build
	dotnet pack $(CORE_PROJECT) --configuration $(CONFIG) --no-build --output $(PACKAGES_DIR)

publish: pack
	@test -n "$(NUGET_API_KEY)" || (echo "NUGET_API_KEY is required" && exit 1)
	dotnet nuget push '$(PACKAGES_DIR)/*.nupkg' \
		--skip-duplicate \
		--api-key $(NUGET_API_KEY) \
		--source $(NUGET_SOURCE)

ci: coverage
