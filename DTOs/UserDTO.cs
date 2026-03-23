using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.DTOs
{
    public class CreateUserDTO
    {
        [Required]
        public string fullName { get; set; } = string.Empty;
        [Required]
        public string UserName { get; set; } = string.Empty;
        [Required]
        [DataType(DataType.Password)]
        //[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Password must be grate than 8 carecter and at least have one uper case and lower case and number and special chraecter")]
        public string Password { get; set; } = string.Empty;
        [Required]
        public int userId { get; set; }
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
        [Phone]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone must be exactly 11 digits")]
        public string PhoneNumber { get; set; } = string.Empty;



    }

    public class RestlocaLPasswordDTO
    {
        [Required]
        public string user_id { get; set; } = string.Empty;
        [Required]

        public String Token { get; set; } = string.Empty;
        [Required]

        public string New_Password { get; set; } = string.Empty;
    }





    public class LoginUserDTO
    {
        [Required]
        public string UserName { get; set; } = "akartaha434@gmail.com";
        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "Akar12345@@";
    }

    public class UpdateProfileDTO
    {

        public string? fullName { get; set; }

        [Phone]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone must be exactly 11 digits")]
        public string? PhoneNumber { get; set; }
    }


    public class ChangeEmailDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string NewEmail { get; set; } = string.Empty;
    }


    public class GetUserRoleDTO
    {
        public string UserID { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IsConfirmEmail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public List<string> Role { get; set; } = new();
        public string IsActivied { get; set; } = string.Empty;

    }

    public class GetUserDTO
    {
        public string full_name { get; set; } = string.Empty;
        public string user_name { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string phone_number { get; set; } = string.Empty;
    }


}