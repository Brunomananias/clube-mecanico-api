using ClubeMecanico_API.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YourProject.DTOs
{
    // Response DTOs
    public class UsuarioResponse
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public int Tipo { get; set; }
        public string NomeCompleto { get; set; }
        public string? CPF { get; set; }
        public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public bool Ativo { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? UltimoLogin { get; set; }
        public List<EnderecoResponse> Enderecos { get; set; } = new List<EnderecoResponse>();

        public UsuarioResponse(Usuario usuario)
        {
            Id = usuario.Id;
            Email = usuario.Email;
            Tipo = usuario.Tipo;
            NomeCompleto = usuario.Nome_Completo;
            CPF = usuario.CPF;
            Telefone = usuario.Telefone;
            DataNascimento = usuario.Data_Nascimento;
            Ativo = usuario.Ativo;
            DataCadastro = usuario.Data_Cadastro;
            UltimoLogin = usuario.UltimoLogin;

            if (usuario.Enderecos != null)
            {
                Enderecos = usuario.Enderecos
                    .Where(e => e.Ativo)
                    .Select(e => new EnderecoResponse(e))
                    .ToList();
            }
        }
    }

    public class EnderecoResponse
    {
        public int Id { get; set; }
        public string? CEP { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string Tipo { get; set; }
        public bool Ativo { get; set; }
        public DateTime DataCadastro { get; set; }
        public DateTime? DataAtualizacao { get; set; }

        public EnderecoResponse(Endereco endereco)
        {
            Id = endereco.Id;
            CEP = endereco.CEP;
            Logradouro = endereco.Logradouro;
            Numero = endereco.Numero;
            Complemento = endereco.Complemento;
            Bairro = endereco.Bairro;
            Cidade = endereco.Cidade;
            Estado = endereco.Estado;
            Tipo = endereco.Tipo;
            Ativo = endereco.Ativo;
            DataCadastro = endereco.DataCadastro;
            DataAtualizacao = endereco.DataAtualizacao;
        }
    }

    // Request DTOs
    public class CreateUsuarioRequest
    {
        public string Email { get; set; }
        public string Senha { get; set; }
        public string NomeCompleto { get; set; }
        public string? CPF { get; set; }
        public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public int? Tipo { get; set; } // 1 = Aluno, 2 = Admin
    }

    public class UpdateUsuarioRequest
    {
        public string? NomeCompleto { get; set; }
        public string? CPF { get; set; }
        public string? Telefone { get; set; }
        public DateTime? DataNascimento { get; set; }
        public bool? Ativo { get; set; }
    }

    public class CreateEnderecoRequest
    {
        public string CEP { get; set; }
        public string Logradouro { get; set; }
        public string Numero { get; set; }
        public string? Complemento { get; set; }
        public string Bairro { get; set; }
        public string Cidade { get; set; }
        public string Estado { get; set; }
        public string? Tipo { get; set; }
    }

    public class UpdateEnderecoRequest
    {
        public string? CEP { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Tipo { get; set; }
        public bool? Ativo { get; set; }
    }
}