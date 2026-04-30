SOLUTION       := NGql.sln
CORE_PROJECT   := src/Core/Core.csproj
TEST_PROJECTS  := tests/Core.Tests/Core.Tests.csproj tests/Core.IntegrationTests/Core.IntegrationTests.csproj
CONFIG         := Release
ARTIFACTS      := $(CURDIR)/artifacts
COVERLET_DIR   := $(ARTIFACTS)/test-results/coverlet
JUNIT_DIR      := $(ARTIFACTS)/test-results/junit
COVERAGE_DIR   := $(ARTIFACTS)/coverage-report
PACKAGES_DIR   := $(ARTIFACTS)/packages
NUGET_SOURCE   := https://api.nuget.org/v3/index.json
NUGET_API_KEY  ?=

.DEFAULT_GOAL := test
.PHONY: help clean restore build test coverage report pack publish ci rebuild tools

help:
	@echo "Targets:"
	@echo "  clean      Remove artifacts/ and bin/obj output"
	@echo "  restore    dotnet restore $(SOLUTION)"
	@echo "  build      restore + dotnet build (-c $(CONFIG))"
	@echo "  test       build + run tests with coverage (cobertura + junit)"
	@echo "  report     Generate HTML coverage report + badges from existing cobertura"
	@echo "  coverage   test + report"
	@echo "  pack       build + dotnet pack $(CORE_PROJECT) -> $(PACKAGES_DIR)"
	@echo "  publish    pack + push *.nupkg to NuGet (needs NUGET_API_KEY)"
	@echo "  ci         clean + coverage (used by GitHub Actions; always starts fresh)"
	@echo "  rebuild    clean + test (force-clean local rebuild)"
	@echo "  tools      Install dotnet-reportgenerator-globaltool if missing"

clean:
	rm -rf $(ARTIFACTS)
	find . -type d \( -name bin -o -name obj \) -not -path "./.git/*" -prune -exec rm -rf {} +

restore:
	dotnet restore $(SOLUTION)

build: restore
	dotnet build $(SOLUTION) --configuration $(CONFIG) --no-restore

test: build
	@mkdir -p $(COVERLET_DIR)
	@count=$$(echo $(TEST_PROJECTS) | wc -w | tr -d ' '); \
	idx=0; \
	for proj in $(TEST_PROJECTS); do \
		idx=$$((idx + 1)); \
		name=$$(basename $$proj .csproj); \
		echo "==> Running $$name ($$idx/$$count)"; \
		if [ $$idx -eq $$count ]; then \
			fmt="cobertura"; \
		else \
			fmt="json"; \
		fi; \
		if [ $$idx -gt 1 ]; then \
			merge_file=$$(ls $(COVERLET_DIR)/coverage.*.json 2>/dev/null | head -1); \
			merge="/p:MergeWith=$$merge_file"; \
		else \
			merge=""; \
		fi; \
		dotnet test $$proj \
			--configuration $(CONFIG) \
			--no-build \
			--results-directory $(ARTIFACTS)/test-results \
			--logger "junit;LogFilePath=$(JUNIT_DIR)/$$name.{framework}.xml;MethodFormat=Class;FailureBodyFormat=Verbose" \
			/p:CollectCoverage=true \
			/p:CoverletOutputFormat=$$fmt \
			/p:CoverletOutput=$(COVERLET_DIR)/coverage \
			/p:ExcludeByFile=\"*.Generated.cs\" \
			$$merge || exit $$?; \
	done

tools:
	@command -v reportgenerator >/dev/null 2>&1 || dotnet tool install -g dotnet-reportgenerator-globaltool
	@command -v dotnet-gitversion >/dev/null 2>&1 || dotnet tool install -g GitVersion.Tool

report: tools
	reportgenerator \
		"-reports:$(COVERLET_DIR)/*.cobertura.xml" \
		"-targetdir:$(COVERAGE_DIR)" \
		"-reporttypes:HtmlInline_AzurePipelines;Badges"

coverage: test report

pack: build tools
	@base=$$(dotnet-gitversion /showvariable MajorMinorPatch); \
	label=$$(dotnet-gitversion /showvariable PreReleaseLabel); \
	commits=$$(dotnet-gitversion /showvariable CommitsSinceVersionSource); \
	if [ -n "$$label" ]; then \
		version="$$base-$$label.$$commits"; \
	else \
		version="$$base"; \
	fi; \
	echo "==> Packing $(CORE_PROJECT) as $$version"; \
	dotnet pack $(CORE_PROJECT) --configuration $(CONFIG) --no-build --output $(PACKAGES_DIR) \
		/p:PackageVersion=$$version \
		/p:UpdateVersionProperties=false

publish: pack
	@test -n "$(NUGET_API_KEY)" || (echo "NUGET_API_KEY is required" && exit 1)
	dotnet nuget push '$(PACKAGES_DIR)/*.nupkg' \
		--skip-duplicate \
		--api-key $(NUGET_API_KEY) \
		--source $(NUGET_SOURCE)

ci:
	$(MAKE) clean
	$(MAKE) coverage

rebuild:
	$(MAKE) clean
	$(MAKE) test
