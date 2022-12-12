defmodule EsDemo.MixProject do
  use Mix.Project

  def project do
    [
      app: :es_demo,
      version: "0.1.0",
      elixir: "~> 1.12",
      start_permanent: Mix.env() == :prod,
      deps: deps()
    ]
  end

  # Run "mix help compile.app" to learn about applications.
  def application do
    [
      extra_applications: [:logger, :crypto, :runtime_tools],
      mod: {EsDemo.Application, []}
    ]
  end

  # Run "mix help deps" to learn about dependencies.
  defp deps do
    [
      # {:dep_from_hexpm, "~> 0.3.0"},
      {:reactive, git: "https://github.com/DigitalCreationAb/reactive.git"},
      {:elixir_uuid, "~> 1.2"},
      {:ex_doc, "~> 0.29.0", only: :dev, runtime: false},
      {:credo, "~> 1.6", only: [:dev, :test], runtime: false}
    ]
  end
end
