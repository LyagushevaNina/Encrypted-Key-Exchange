#pragma warning disable CA1031
#pragma warning disable CA1303
#pragma warning disable CA2213
#pragma warning disable CS8618
#pragma warning disable IDE0058

using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Bob;

public partial class BobForm : Form
{
	private static readonly BigInteger p = 104729;
	private static readonly BigInteger g = 5;

	private TextBox textBoxMessages;
	private TextBox textBoxInput;
	private Button buttonSend;
	private BigInteger privateKey;
	private BigInteger sharedSecret;
	private TcpClient client;
	private NetworkStream stream;

	public BobForm ()
	{
		InitializeComponent("Bob");
		ConnectToAlice();
	}

	private void ConnectToAlice ()
	{
		privateKey = GeneratePrivateKey();
		AppendMessage($"Private key generated: {privateKey}");

		BigInteger publicKey = BigInteger.ModPow(g, privateKey, p);

		client = new TcpClient("127.0.0.1", 5000);
		stream = client.GetStream();
		AppendMessage("Connection established");

		BigInteger alicePublicKey = ReceiveBigInteger(stream);
		AppendMessage($"Public key received: {alicePublicKey}");

		SendBigInteger(stream, publicKey);
		AppendMessage($"Public key sent: {publicKey}");

		sharedSecret = BigInteger.ModPow(alicePublicKey, privateKey, p);
		AppendMessage($"Shared secret calculated: {sharedSecret}");

		ReceiveMessages();
	}

	private async void ReceiveMessages ()
	{
		while (true)
		{
			if (stream == null)
			{
				continue;
			}

			try
			{
				byte [] buffer = new byte [256];
				int bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(true);
				if (bytesRead > 0)
				{
					byte [] trimmedBuffer = new byte [bytesRead];
					Array.Copy(buffer, trimmedBuffer, bytesRead);

					string decryptedMessage = DecryptMessage(trimmedBuffer, sharedSecret);
					AppendMessage($"Alice: {decryptedMessage}");
				}
			}
			catch (Exception ex)
			{
				AppendMessage($"Error receiving message: {ex.Message}");
				break;
			}
		}
	}

	private void ButtonSend_Click (object? sender, EventArgs e)
	{
		string message = textBoxInput.Text;
		byte [] encryptedMessage = EncryptMessage(message, sharedSecret);
		stream.Write(encryptedMessage, 0, encryptedMessage.Length);
		AppendMessage($"Bob: {message}");
		textBoxInput.Clear();
	}

	private void AppendMessage (string message)
	{
		textBoxMessages.AppendText(message + Environment.NewLine);
	}

	private static BigInteger GeneratePrivateKey ()
	{
		byte [] randomBytes = new byte [32];
		using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
		{
			rng.GetBytes(randomBytes);
		}

		BigInteger privateKey = new(randomBytes);
		privateKey = BigInteger.Abs(privateKey) % p;

		return privateKey;
	}

	private static void SendBigInteger (NetworkStream stream, BigInteger value)
	{
		byte [] data = value.ToByteArray();
		stream.Write(data, 0, data.Length);
	}

	private static BigInteger ReceiveBigInteger (NetworkStream stream)
	{
		byte [] data = new byte [256];
		int bytesRead = stream.Read(data, 0, data.Length);
		byte [] trimmedData = new byte [bytesRead];
		Array.Copy(data, trimmedData, bytesRead);

		return new BigInteger(trimmedData);
	}

	private static byte [] EncryptMessage (string message, BigInteger key)
	{
		byte [] messageBytes = Encoding.UTF8.GetBytes(message);
		byte [] keyBytes = key.ToByteArray();

		for (int i = 0; i < messageBytes.Length; i++)
		{
			messageBytes [i] ^= keyBytes [i % keyBytes.Length];
		}

		return messageBytes;
	}

	private static string DecryptMessage (byte [] encryptedMessage, BigInteger key)
	{
		byte [] keyBytes = key.ToByteArray();

		for (int i = 0; i < encryptedMessage.Length; i++)
		{
			encryptedMessage [i] ^= keyBytes [i % keyBytes.Length];
		}

		return Encoding.UTF8.GetString(encryptedMessage);
	}

	private void InitializeComponent (string name)
	{
		AutoScaleMode = AutoScaleMode.Dpi;
		FormBorderStyle = FormBorderStyle.Sizable;
		Text = name;

		TableLayoutPanel mainLayout = new()
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2
		};

		mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 80)); // Окно с логами - 80% высоты
		mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); // Панель ввода текста и кнопки - 20% высоты

		textBoxMessages = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			TabIndex = 1,
			ScrollBars = ScrollBars.Vertical
		};

		TableLayoutPanel inputPanel = new()
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1
		};

		inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80)); // Поле ввода текста - 80% ширины
		inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); // Кнопка - 20% ширины

		textBoxInput = new TextBox
		{
			Dock = DockStyle.Fill,
			TabIndex = 0
		};

		buttonSend = new Button
		{
			Dock = DockStyle.Fill,
			TabIndex = 2,
			Text = "Send",
			UseVisualStyleBackColor = true
		};

		inputPanel.Controls.Add(textBoxInput, 0, 0);
		inputPanel.Controls.Add(buttonSend, 1, 0);

		mainLayout.Controls.Add(textBoxMessages, 0, 0);
		mainLayout.Controls.Add(inputPanel, 0, 1);

		Controls.Add(mainLayout);

		ClientSize = new Size(700, 500);
		MinimumSize = new Size(300, 200);

		FormClosing += OnFormClosing;
		buttonSend.Click += ButtonSend_Click;
		textBoxInput.KeyDown += TextBoxInput_KeyDown;
	}

	private void OnFormClosing (object? sender, FormClosingEventArgs e)
	{
		Environment.Exit(0);
	}

	private void TextBoxInput_KeyDown (object? sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Enter)
		{
			e.SuppressKeyPress = true;
			buttonSend.PerformClick();
		}
	}
}

internal static class Program
{
	[STAThread]
	private static void Main ()
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		using BobForm form = new();
		Application.Run(form);
	}
}
