defmodule EsDemo.BankAccounts.AccountWithBehavior do
  @moduledoc false

  use Eventize.EventSourcedProcess

  def child_spec(%{:id => id} = data) do
    %{
      id: id,
      start: {__MODULE__, :start_link, [data]},
      restart: :transient
    }
  end

  def start_link(%{:id => id} = data) do
    GenServer.start_link(
      __MODULE__,
      data,
      name: {:global, id}
    )
  end

  @impl true
  def start(_) do
    {EsDemo.BankAccounts.AccountWithBehavior.NotOpened, %{balance: 0}}
  end

  defmodule NotOpened do
    def execute_call({:open, %{:customer => customer}}, _from, %{
          :id => id
        }) do
      {[{:account_opened, %{id: id, customer: customer, time_stamp: DateTime.utc_now()}}],
       %{id: id, customer: customer}}
    end

    def execute_call(_command, _state) do
      {:error, "This account doesn't exist"}
    end
  end

  defmodule Opened do
    def execute_call({:deposit, %{:amount => amount}}, _from, %{
          :id => id,
          :state => %{:balance => current_balance}
        })
        when amount > 0 do
      {[{:money_deposited_to_account, %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}],
       %{id: id, balance: current_balance + amount}}
    end

    def execute_call({:deposit, %{:amount => _amount}}, _from, _context) do
      {:error, "Can't deposit negative amount"}
    end

    def execute_call({:withdraw, %{:amount => amount}}, _from, %{
          :id => id,
          :state => %{:balance => current_balance}
        })
        when current_balance >= amount and amount > 0 do
      {[
         {:money_withdrawn_from_account,
          %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
       ], %{id: id, balance: current_balance - amount}}
    end

    def execute_call({:withdraw, %{:amount => amount}}, _from, _context) when amount <= 0 do
      {:error, "Can't withdraw negative amount"}
    end

    def execute_call({:withdraw, _cmd}, _from, _context) do
      {:error, "Not enough money in account"}
    end

    def execute_call(:close, _from, %{
          :id => id,
          :state => %{:balance => current_balance}
        })
        when current_balance == 0 do
      {[{:account_closed, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
    end

    def execute_call(:close, _from, %{
          :state => %{:balance => current_balance}
        })
        when current_balance > 0 do
      {:error, "Can't close account with money left"}
    end

    def execute_call(:close, _from, %{
          :state => %{:balance => current_balance}
        })
        when current_balance < 0 do
      {:error, "Can't close account with outstanding dept"}
    end

    def execute_call(:suspend, _from, %{
          :id => id
        }) do
      {[{:account_suspended, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
    end

    def execute_call(:get_balance, _from, %{:id => id, :state => %{:balance => current_balance}}) do
      %{id: id, balance: current_balance}
    end

    def execute_cast({:withdraw, %{:amount => amount}}, _from, %{
          :id => id,
          :state => %{:balance => current_balance}
        })
        when current_balance >= amount do
      [
        {:money_withdrawn_from_account, %{id: id, amount: amount, time_stamp: DateTime.utc_now()}}
      ]
    end

    def execute_cast({:withdraw, _cmd}, _from, _context) do
    end
  end

  defmodule Closed do
    def execute_call({:deposit, %{}}, _context) do
      {:error, "Can't deposit money to a closed account"}
    end

    def execute_call({:withdraw, %{}}, _context) do
      {:error, "Can't withdraw money from a closed account"}
    end

    def execute_call(:close, _context) do
      {:error, "This account is already closed"}
    end

    def execute_call(:suspend, %{}) do
      {:error, "Can't suspend a closed account"}
    end

    def execute_call(:get_balance, %{:id => id}) do
      %{id: id, balance: 0}
    end
  end

  defmodule Suspended do
    def execute_call(:restore, %{:id => id}) do
      {[{:account_restored, %{id: id, time_stamp: DateTime.utc_now()}}], %{id: id}}
    end

    def execute_call({:deposit, %{}}, _state) do
      {:error, "Can't deposit money into a suspended account"}
    end

    def execute_call({:withdraw, %{}}, _state) do
      {:error, "Can't withdraw money from a suspended account"}
    end

    def execute_call(:close, _state) do
      {:error, "Can't close a suspended account"}
    end

    def execute_call(:suspend, _state) do
      {:error, "This account is already suspended"}
    end

    def execute_call(:get_balance, %{:id => id}) do
      %{id: id, balance: 0}
    end
  end

  @impl true
  def apply_event(state, {:account_opened, %{:customer => customer}}) do
    {Map.put(state, :customer, customer), EsDemo.BankAccounts.AccountWithBehavior.Opened}
  end

  def apply_event(state, {:money_deposited_to_account, %{:amount => amount}}) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance + amount end)
  end

  def apply_event(state, {:money_withdrawn_from_account, %{:amount => amount}}) do
    Map.update(state, :balance, amount, fn current_balance -> current_balance - amount end)
  end

  def apply_event(state, {:account_closed, _data}) do
    {state, EsDemo.BankAccounts.AccountWithBehavior.Closed}
  end

  def apply_event(state, {:account_suspended, _data}) do
    {state, EsDemo.BankAccounts.AccountWithBehavior.Suspended}
  end

  def apply_event(state, {:account_restored, _data}) do
    {state, EsDemo.BankAccounts.AccountWithBehavior.Opened}
  end

  @impl true
  def cleanup({:account_closed, _data}, _state) do
    :stop
  end
end
