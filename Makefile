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

TOOL_PROJECT   := tools/Tool/Tool.csproj

# ── Skill publishing ──────────────────────────────────────────────────────────────────────────
# The NGql Skill ships independently of the library on its own SemVer stream (see
# .claude/skills/ngql-local/GitVersion.yml). It's published into a separate catalog repo
# (default dolifer/claude-plugins) under one of two channels:
#   preview -> plugins/ngql-preview
#   stable  -> plugins/ngql
# Override CATALOG_REPO if you want to test the publish flow against a fork.
SKILL_SRC          := .claude/skills/ngql-local
SKILL_STAGE_DIR    := $(ARTIFACTS)/skill
SKILL_GITVERSION   := $(SKILL_SRC)/GitVersion.yml
CATALOG_REPO       ?= dolifer/claude-plugins
CATALOG_REMOTE     ?= git@github.com:$(CATALOG_REPO).git
CATALOG_BRANCH     ?= main
SKILL_BOT_NAME     ?= NGql Release Bot
SKILL_BOT_EMAIL    ?= noreply@github.com
DRY_RUN            ?=

.DEFAULT_GOAL := test
.PHONY: help clean restore build test coverage report pack publish ci rebuild tools skill-eval skill-version skill-stage skill-publish-preview skill-publish-stable

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
	@echo "  skill-eval               Run NGql Skill fixtures (or pass SNIPPET=path/to/file.cs)"
	@echo "  skill-version            Print the computed Skill SemVer (CHANNEL=preview|stable, default preview)"
	@echo "  skill-stage              Stage the Skill into $(SKILL_STAGE_DIR)/<channel>/ for inspection"
	@echo "  skill-publish-preview    Stage + publish to the preview channel (DRY_RUN=1 to skip the git push)"
	@echo "  skill-publish-stable     Stage + publish to the stable channel (DRY_RUN=1 to skip the git push)"

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

skill-eval:
ifdef SNIPPET
	dotnet run --project $(TOOL_PROJECT) -f net10.0 -- $(SNIPPET)
else
	dotnet run --project $(TOOL_PROJECT) -f net10.0 -- fixtures
endif

# Resolve the channel from CHANNEL=preview|stable. Default preview because that's the safer
# (and far more common) operation — stable is the explicit one.
CHANNEL ?= preview

# VERSION can be overridden at the command line (useful for testing or for matching an
# external tag). Without an override, ask GitVersion using the Skill-specific config — the
# `feature` branch entry there is set to `mode: ContinuousDelivery`, so every commit ticks
# the prerelease number (`1.0.1-preview.1` -> `.2` -> `.3`...). Distinct catalog versions
# per publish without Makefile-side composition.
ifndef VERSION
SKILL_VERSION := $$(dotnet-gitversion /config $(SKILL_GITVERSION) /showvariable SemVer)
else
SKILL_VERSION := $(VERSION)
endif

skill-version:
	@command -v dotnet-gitversion >/dev/null 2>&1 || (echo "dotnet-gitversion not on PATH; run \`make tools\` first" && exit 1)
	@dotnet-gitversion /config $(SKILL_GITVERSION) /showvariable SemVer

skill-stage: tools
	@command -v python3 >/dev/null 2>&1 || (echo "python3 is required" && exit 1)
	@version="$(SKILL_VERSION)"; \
	out="$(SKILL_STAGE_DIR)/$(CHANNEL)"; \
	rm -rf $$out; \
	mkdir -p $$out; \
	echo "==> Staging Skill (channel=$(CHANNEL), version=$$version) -> $$out"; \
	python3 $(SKILL_SRC)/scripts/stage_skill.py \
		--channel $(CHANNEL) \
		--version $$version \
		--source $(SKILL_SRC) \
		--out $$out

# Internal: shared by skill-publish-preview and skill-publish-stable. Don't invoke directly —
# the channel-specific public targets set CHANNEL and force a fresh stage step.
_skill-publish: skill-stage
	@command -v git >/dev/null 2>&1 || (echo "git is required" && exit 1)
	@set -e; \
	version="$(SKILL_VERSION)"; \
	stage="$(SKILL_STAGE_DIR)/$(CHANNEL)"; \
	clone="$(SKILL_STAGE_DIR)/catalog-$(CHANNEL)"; \
	plugin_name=$$([ "$(CHANNEL)" = "stable" ] && echo "ngql" || echo "ngql-preview"); \
	echo "==> Cloning $(CATALOG_REMOTE) (branch $(CATALOG_BRANCH)) -> $$clone"; \
	rm -rf $$clone; \
	git clone --depth 1 --branch $(CATALOG_BRANCH) $(CATALOG_REMOTE) $$clone; \
	echo "==> Copying staged plugin into the catalog clone"; \
	mkdir -p $$clone/plugins/$$plugin_name; \
	rm -rf $$clone/plugins/$$plugin_name/skills $$clone/plugins/$$plugin_name/.claude-plugin; \
	cp -R $$stage/plugins/$$plugin_name/. $$clone/plugins/$$plugin_name/; \
	echo "==> Mirroring plugin.json into _data/plugins/ for Pages version display"; \
	mkdir -p $$clone/_data/plugins; \
	cp $$stage/plugins/$$plugin_name/.claude-plugin/plugin.json $$clone/_data/plugins/$$plugin_name.json; \
	cd $$clone && \
	  git add plugins/$$plugin_name _data/plugins/$$plugin_name.json && \
	  if git diff --cached --quiet; then \
	    echo "==> No catalog changes for $$plugin_name@$$version (nothing to publish)"; \
	    exit 0; \
	  fi; \
	  git -c user.name="$(SKILL_BOT_NAME)" -c user.email="$(SKILL_BOT_EMAIL)" \
	      -c commit.gpgsign=false -c tag.gpgsign=false \
	      commit -m "Sync $$plugin_name@$$version from dolifer/NGql"; \
	  if [ -n "$(DRY_RUN)" ]; then \
	    echo "==> DRY_RUN=1, skipping git push (commit prepared in $$clone)"; \
	  else \
	    git push origin $(CATALOG_BRANCH); \
	    echo "==> Published $$plugin_name@$$version to $(CATALOG_REPO)"; \
	  fi

skill-publish-preview:
	$(MAKE) _skill-publish CHANNEL=preview

skill-publish-stable:
	$(MAKE) _skill-publish CHANNEL=stable

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
		/p:UpdateVersionProperties=false; \
	echo "==> Packing $(TOOL_PROJECT) as $$version (lockstep with NGql.Core)"; \
	dotnet pack $(TOOL_PROJECT) --configuration $(CONFIG) --output $(PACKAGES_DIR) \
		/p:PackageVersion=$$version \
		/p:Version=$$version \
		/p:InformationalVersion=$$version \
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
