defmodule EsDemoTest do
  use ExUnit.Case
  doctest EsDemo

  test "greets the world" do
    assert EsDemo.hello() == :world
  end
end
