PROJECT_NAME ?= mkv-rename-wizard
IMAGE_OWNER ?= nickspaghetti/${PROJECT_NAME}
REGISTRY_URL = ghcr.io/${IMAGE_OWNER}
IMAGE_NAME ?= ${PROJECT_NAME}
RELEASE_TAG ?= v0
IMAGE_VERSION ?= local
DEV_IMAGE_VERSION ?= local-dev
IMAGE_FULL_NAME = ${REGISTRY_URL}/${IMAGE_NAME}
DOCKER_BUILDX_FLAG_PLATFORM ?= linux/amd64
DOCKER_BUILDX_FLAG_OUTPUT ?= docker

# Build docker image by spec
# Parameters:
#	$1 - Docker build target (production, development)
#	$2 - Docker image tag (local, local-dev)
define build_image_by_spec
	@docker buildx build \
		--target $1 \
		--tag $2 \
		--pull \
		--output=type=${DOCKER_BUILDX_FLAG_OUTPUT} \
		--platform ${DOCKER_BUILDX_FLAG_PLATFORM} \
		.
endef

.DEFAULT_GOAL := help

help:				## Show help menu
	@grep -E '^[a-zA-Z_\%-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

clean:				## Remove Rust build artifacts (mkv-rename-tui/target)
	@docker compose run --rm --no-deps dev cargo clean --manifest-path mkv-rename-tui/Cargo.toml

remove:				## Remove previously built Docker images
	@docker rmi ${IMAGE_FULL_NAME}:${IMAGE_VERSION} ${IMAGE_FULL_NAME}:${DEV_IMAGE_VERSION} 2>/dev/null || true

build:				## Build production NativeAOT CLI Docker image
	@$(call build_image_by_spec,production,${IMAGE_FULL_NAME}:${IMAGE_VERSION})

build-dev:			## Build development polyglot Docker image (.NET 10 + Rust + Zig + trace/gcdump)
	@$(call build_image_by_spec,development,${IMAGE_FULL_NAME}:${DEV_IMAGE_VERSION})

dev-shell:			## Open interactive bash shell inside the dev container
	@docker compose run --rm dev /bin/bash

aspire-up:			## Start the Aspire Dashboard container (http://localhost:18888)
	@docker compose up -d aspire-dashboard
	@echo "Aspire Dashboard running at http://localhost:18888 (OTLP gRPC on localhost:4317)"

aspire-down:			## Stop the Aspire Dashboard container
	@docker compose down

test:				## Run all .NET and Rust unit tests inside the dev container
	@docker compose run --rm --no-deps dev bash -c '\
		set -euo pipefail; \
		dotnet test MkvRenameWizard/MkvRenameWizard.sln -c Release; \
		cargo test --manifest-path mkv-rename-tui/Cargo.toml'

tui:				## Run the Rust OpenTUI app inside the dev container against /sandbox/media
	@docker compose run --rm --no-deps dev bash -c '\
		set -euo pipefail; \
		rm -rf /sandbox/media && mkdir -p /sandbox/media; \
		if [ -d /workspace/Tests/Integration/TestMedia ]; then \
			cp -r /workspace/Tests/Integration/TestMedia/. /sandbox/media/; \
		fi; \
		dotnet build /workspace/MkvRenameWizard/MkvRenameWizard.Cli/MkvRenameWizard.Cli.csproj -c Profile --nologo; \
		cargo run --manifest-path /workspace/mkv-rename-tui/Cargo.toml -- /sandbox/media'

test-sandbox:			## Run safe CLI scan & stdio smoke test against a disposable copy of TestMedia in /sandbox
	@docker compose run --rm --no-deps dev bash -c '\
		set -euo pipefail; \
		rm -rf /sandbox/media && mkdir -p /sandbox/media; \
		if [ -d /workspace/Tests/Integration/TestMedia ]; then \
			cp -r /workspace/Tests/Integration/TestMedia/. /sandbox/media/; \
		fi; \
		dotnet build /workspace/MkvRenameWizard/MkvRenameWizard.Cli/MkvRenameWizard.Cli.csproj -c Profile --nologo; \
		CLI=/workspace/MkvRenameWizard/MkvRenameWizard.Cli/bin/Profile/net10.0/mkv-rename-cli.dll; \
		dotnet "$$CLI" scan /sandbox/media --json | head -c 400; echo ""; \
		printf "{\"id\":\"sandbox-1\",\"method\":\"hello\"}\n" | dotnet "$$CLI" serve --stdio'

profile:			## Collect dotnet-trace CPU profile inside the dev container
	@docker compose run --rm --no-deps dev bash /workspace/Scripts/profile-cli.sh /workspace/.profiles

which-image:			## Image identifier helper
	@echo ${IMAGE_FULL_NAME}:${IMAGE_VERSION}

run:				## Run the production CLI container
	@docker run --rm ${IMAGE_FULL_NAME}:${IMAGE_VERSION}

shell:				## Shell into the production container
	@docker run -it --rm --entrypoint /bin/bash ${IMAGE_FULL_NAME}:${IMAGE_VERSION}

pull:				## Pull image and mark as local
	@docker pull ${IMAGE_FULL_NAME}:${IMAGE_VERSION}
	@docker tag ${IMAGE_FULL_NAME}:${IMAGE_VERSION} ${IMAGE_FULL_NAME}:local

push:				## Push image to artifact registry
	@docker tag ${IMAGE_FULL_NAME}:local ${IMAGE_FULL_NAME}:${IMAGE_VERSION}
	@docker push ${IMAGE_FULL_NAME}:${IMAGE_VERSION}

release:			## Release production image to registry
	@$(call build_image_by_spec,production,${IMAGE_FULL_NAME}:${RELEASE_TAG})
