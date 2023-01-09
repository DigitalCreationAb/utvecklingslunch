SHELL := /bin/bash
.ONESHELL:
.DELETE_ON_ERROR:
.SHELLFLAGS := -eu -o pipefail -c
MAKEFLAGS += --warn-undefined-variables 
MAKEFLAGS += --no-builtin-rules

HEX_API_KEY ?=

ifeq ($(OS), Windows_NT)
    DETECTED_OS := Windows
endif

.PHONY: build
build: restore
	mix compile

.PHONY: restore
restore:
	mix deps.get

.PHONY: start
start:
	docker run -d --rm --name elixir_es_demo -it -p 2113:2113 -e EVENTSTORE_INSECURE=True -e EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=True eventstore/eventstore:latest

.PHONY: stop
stop:
	docker stop elixir_es_demo
